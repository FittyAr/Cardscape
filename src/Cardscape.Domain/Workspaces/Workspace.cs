using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces.Errors;
using Cardscape.Domain.Workspaces.Events;
using static Cardscape.Domain.Workspaces.Errors.WorkspaceErrors;

namespace Cardscape.Domain.Workspaces;

/// <summary>
/// A workspace is the top-level container for boards, members, and
/// (eventually) integrations. Every workspace has exactly one
/// owner (the user that created it).
/// </summary>
public sealed class Workspace : AggregateRoot<WorkspaceId>
{
    public WorkspaceName Name { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public bool IsArchived { get; private set; }

    /// <summary>Geographic data-residency region. When the
    /// deployment is configured with a single region (see
    /// <c>Cardscape:Deployment:Region</c>), every write to
    /// this workspace must pass through
    /// <see cref="GuardRegion"/>; a mismatch surfaces as
    /// <see cref="Errors.WorkspaceErrors.RegionMismatch"/>.</summary>
    public Region Region { get; private set; } = Region.Unspecified;

    private readonly List<WorkspaceMember> _members = [];
    public IReadOnlyCollection<WorkspaceMember> Members => _members.AsReadOnly();

    // EF Core.
    private Workspace() { }

    private Workspace(WorkspaceId id, WorkspaceName name, Guid ownerId, Region region, DateTimeOffset at)
    {
        Id = id;
        Name = name;
        OwnerId = ownerId;
        Region = region;
        CreatedAt = at;

        // The owner is automatically an Admin member.
        _members.Add(WorkspaceMember.Create(id, ownerId, WorkspaceRole.Admin, at));
    }

    /// <summary>Factory: create a new workspace. The owner is added as the first member.</summary>
    public static Result<Workspace> Create(
        WorkspaceId id,
        WorkspaceName name,
        Guid ownerId,
        Region region,
        DateTimeOffset at)
    {
        if (ownerId == Guid.Empty)
        {
            return Result.Failure<Workspace>(DomainError.Validation(
                "workspaces.owner_required",
                "Workspace owner id is required."));
        }

        var workspace = new Workspace(id, name, ownerId, region, at);
        workspace.AddDomainEvent(new WorkspaceCreated(id, ownerId, name, at));
        return Result.Success(workspace);
    }

    /// <summary>Renames the workspace.</summary>
    public Result Rename(WorkspaceName newName, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(InsufficientPermissions);
        }

        if (newName.Value == Name.Value)
        {
            return Result.Success();
        }

        Name = newName;
        UpdatedAt = at;
        AddDomainEvent(new WorkspaceRenamed(Id, newName, at));
        return Result.Success();
    }

    /// <summary>Archives the workspace. New boards cannot be created inside an archived workspace.</summary>
    public void Archive(DateTimeOffset at)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        UpdatedAt = at;
        AddDomainEvent(new WorkspaceArchived(Id, at));
    }

    /// <summary>Restores an archived workspace. BETA-A2-001 — see test-results/beta/00-FINAL-SUMMARY.md.</summary>
    public void Unarchive(DateTimeOffset at)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        UpdatedAt = at;
        AddDomainEvent(new WorkspaceUnarchived(Id, at));
    }

    /// <summary>Adds a new member.</summary>
    public Result AddMember(Guid userId, WorkspaceRole role, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(InsufficientPermissions);
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return Result.Failure(AlreadyMember);
        }

        _members.Add(WorkspaceMember.Create(Id, userId, role, at));
        UpdatedAt = at;
        AddDomainEvent(new WorkspaceMemberAdded(Id, userId, role, at));
        return Result.Success();
    }

    /// <summary>Removes a member. The owner cannot be removed.</summary>
    public Result RemoveMember(Guid userId, DateTimeOffset at)
    {
        if (userId == OwnerId)
        {
            return Result.Failure(CannotRemoveOwner);
        }

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(NotMember);
        }

        _members.Remove(member);
        UpdatedAt = at;
        AddDomainEvent(new WorkspaceMemberRemoved(Id, userId, at));
        return Result.Success();
    }

    /// <summary>Changes a member's role. Owner cannot be demoted.</summary>
    public Result ChangeMemberRole(Guid userId, WorkspaceRole newRole, DateTimeOffset at)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(NotMember);
        }

        var changeResult = member.ChangeRole(newRole, isOwner: userId == OwnerId);
        if (changeResult.IsFailure)
        {
            return changeResult;
        }

        UpdatedAt = at;
        AddDomainEvent(new WorkspaceMemberRoleChanged(Id, userId, newRole, at));
        return Result.Success();
    }

    /// <summary>True if the user is a member of the workspace.</summary>
    public bool HasMember(Guid userId) => _members.Any(m => m.UserId == userId);

    /// <summary>Owner-only: change the workspace's data-residency region.
    /// Emits <c>WorkspaceRegionChanged</c>. Once the deployment
    /// has a region configured, changing the region of an existing
    /// workspace is a one-way trip — the new region must match
    /// the deployment's region or the next write will fail
    /// <see cref="GuardRegion"/>.</summary>
    public Result SetRegion(Region newRegion, Guid actingUserId, DateTimeOffset at)
    {
        if (actingUserId != OwnerId)
        {
            return Result.Failure(CannotChangeRegion);
        }

        if (newRegion == Region)
        {
            return Result.Success();
        }

        Region = newRegion;
        UpdatedAt = at;
        AddDomainEvent(new WorkspaceRegionChanged(Id, newRegion, at));
        return Result.Success();
    }

    /// <summary>Guards a write against the deployment's configured region.
    /// <list type="bullet">
    ///   <item>If <paramref name="deploymentRegion"/> is
    ///   <see cref="Region.Unspecified"/>, no region gating is
    ///   applied (single-tenant / dev deployments).</item>
    ///   <item>If the workspace is <see cref="Region.Unspecified"/>,
    ///   no gating is applied (the workspace is region-agnostic).</item>
    ///   <item>If the two regions match, the write is allowed.</item>
    ///   <item>Otherwise the guard fails with
    ///   <see cref="Errors.WorkspaceErrors.RegionMismatch"/>.</item>
    /// </list>
    /// </summary>
    public Result GuardRegion(Region deploymentRegion)
    {
        if (deploymentRegion == Region.Unspecified)
        {
            return Result.Success();
        }

        if (Region == Region.Unspecified)
        {
            return Result.Success();
        }

        if (Region != deploymentRegion)
        {
            return Result.Failure(RegionMismatch);
        }

        return Result.Success();
    }
}
