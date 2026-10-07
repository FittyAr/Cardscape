using System.Text.RegularExpressions;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Infrastructure.Scim;

/// <summary>
/// Default <see cref="IScimService"/> implementation. Bridges
/// the SCIM v2 <c>User</c> shape (RFC 7643 + 7644) to the
/// <see cref="User"/> aggregate and the
/// <see cref="WorkspaceMember"/> assignment. The IdP-presented
/// bearer token has already been verified by
/// <c>ScimAuthenticationHandler</c> in the API pipeline; by
/// the time a request lands here, the workspace id is on
/// <c>HttpContext.Items["scim.workspaceId"]</c>.
/// </summary>
public sealed partial class ScimService(
    IRepository<User, UserId> users,
    IUserRepository userRepository,
    IRepository<Workspace, WorkspaceId> workspaces,
    IUnitOfWork unitOfWork,
    IClock clock) : IScimService
{
    private const string ScimGroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    private const string ScimListResponseSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string ScimGroupIdPrefix = "workspace-";

    [GeneratedRegex("^\\s*members\\s*\\[\\s*value\\s+eq\\s+\"(?<id>[0-9a-f-]+)\"\\s*\\]\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex MemberRemovalPath();

    private readonly IRepository<User, UserId> _users = users;

    public async Task<ScimListResponse<ScimGroup>> ListGroupsAsync(
        Guid workspaceId, int startIndex, int count, CancellationToken ct = default)
    {
        // The SCIM token scopes the IdP to a single
        // workspace, so this list is always either 0 or 1
        // group. If the workspace was deleted between token
        // issuance and this call we return an empty
        // list — the IdP will reconcile.
        int normalizedStartIndex = Math.Max(1, startIndex);
        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return new ScimListResponse<ScimGroup>(
                [ScimListResponseSchema], 0, 0, normalizedStartIndex, []);
        }

        // The token-scoped result set has one item. RFC 7644 3.4.2.4:
        // nonpositive count requests totals only, not a default-sized page.
        if (count <= 0 || normalizedStartIndex > 1)
        {
            return new ScimListResponse<ScimGroup>(
                [ScimListResponseSchema], 1, 0, normalizedStartIndex, []);
        }

        IReadOnlyList<ScimGroupMember> members = await BuildMembersAsync(workspace, ct);
        ScimGroup group = new(
            BuildGroupId(workspace.Id.Value),
            [ScimGroupSchema],
            workspace.Name.Value,
            members);

        IReadOnlyList<ScimGroup> page = [group];

        return new ScimListResponse<ScimGroup>(
            [ScimListResponseSchema],
            1,
            page.Count,
            normalizedStartIndex,
            page);
    }

    public async Task<Result<ScimGroup>> CreateGroupAsync(
        Guid workspaceId, ScimGroup group, CancellationToken ct = default)
    {
        // SCIM `POST /Groups` provisions a new workspace
        // owned by the same user that owns the token's
        // workspace — this is the simplest 1:1 mapping and
        // matches the "one organisation, one admin" mental
        // model IdPs have. The input `id` is server-assigned
        // and any value the IdP sent is ignored.
        var parent = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (parent is null)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.workspace_not_found",
                $"Workspace {workspaceId} was not found."));
        }

        var nameResult = WorkspaceName.Create(group.DisplayName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<ScimGroup>(nameResult.Error);
        }

        var createdResult = Workspace.Create(
            WorkspaceId.New(),
            nameResult.Value,
            parent.OwnerId,
            parent.Region,
            clock.UtcNow);
        if (createdResult.IsFailure)
        {
            return Result.Failure<ScimGroup>(createdResult.Error);
        }

        var newWorkspace = createdResult.Value;
        await workspaces.AddAsync(newWorkspace, ct);

        // Best-effort member sync. We log-and-continue on a
        // missing user so a single bad id from the IdP does
        // not abort the whole create; the SCIM spec says
        // the IdP may keep stale references for users that
        // were just off-boarded.
        IReadOnlyList<User> requestedMembers = await LoadValidUsersAsync(group.Members, ct);
        foreach (var user in requestedMembers)
        {
            newWorkspace.AddMember(user.Id.Value, WorkspaceRole.Member, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<ScimGroupMember> members = await BuildMembersAsync(newWorkspace, ct);
        return Result.Success(new ScimGroup(
            BuildGroupId(newWorkspace.Id.Value),
            [ScimGroupSchema],
            newWorkspace.Name.Value,
            members));
    }

    public async Task<Result<ScimGroup>> GetGroupAsync(
        Guid workspaceId, string groupId, CancellationToken ct = default)
    {
        if (!TryParseGroupId(groupId, out Guid groupGuid)
            || groupGuid != workspaceId)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        IReadOnlyList<ScimGroupMember> members = await BuildMembersAsync(workspace, ct);
        return Result.Success(new ScimGroup(
            BuildGroupId(workspace.Id.Value),
            [ScimGroupSchema],
            workspace.Name.Value,
            members));
    }

    public async Task<Result<ScimGroup>> UpdateGroupAsync(
        Guid workspaceId, string groupId, ScimGroup group, CancellationToken ct = default)
    {
        if (!TryParseGroupId(groupId, out Guid groupGuid)
            || groupGuid != workspaceId)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        var nameResult = WorkspaceName.Create(group.DisplayName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<ScimGroup>(nameResult.Error);
        }

        var renameResult = workspace.Rename(nameResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<ScimGroup>(renameResult.Error);
        }

        await ReplaceMembersAsync(workspace, group.Members, ct);

        await unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<ScimGroupMember> members = await BuildMembersAsync(workspace, ct);
        return Result.Success(new ScimGroup(
            BuildGroupId(workspace.Id.Value),
            [ScimGroupSchema],
            workspace.Name.Value,
            members));
    }

    public async Task<Result<ScimGroup>> PatchGroupAsync(
        Guid workspaceId, string groupId, ScimPatchRequest patch, CancellationToken ct = default)
    {
        if (!TryParseGroupId(groupId, out Guid groupGuid)
            || groupGuid != workspaceId)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<ScimGroup>(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        Result<IReadOnlyList<ScimPatchOperation>> normalized = NormalizeGroupPatch(patch, workspace.OwnerId);
        if (normalized.IsFailure)
        {
            return Result.Failure<ScimGroup>(normalized.Error);
        }

        foreach (var op in normalized.Value)
        {
            string opName = op.Op.ToLowerInvariant();

            if (string.Equals(op.Path, "displayName", StringComparison.OrdinalIgnoreCase))
            {
                string? newName = op.Value as string
                    ?? (op.Value is System.Text.Json.JsonElement je
                        && je.ValueKind == System.Text.Json.JsonValueKind.String
                        ? je.GetString()
                        : null);
                if (string.IsNullOrWhiteSpace(newName))
                {
                    continue;
                }

                var nameResult = WorkspaceName.Create(newName);
                if (nameResult.IsFailure)
                {
                    return Result.Failure<ScimGroup>(nameResult.Error);
                }

                var renameResult = workspace.Rename(nameResult.Value, clock.UtcNow);
                if (renameResult.IsFailure)
                {
                    return Result.Failure<ScimGroup>(renameResult.Error);
                }
                continue;
            }

            if (opName == "replace"
                && string.Equals(op.Path, "members", StringComparison.OrdinalIgnoreCase))
            {
                // `replace members` with a new list is
                // treated as a full member-list replace.
                IReadOnlyList<ScimGroupMember> desired = ExtractMembers(op.Value);
                await ReplaceMembersAsync(workspace, desired, ct);
                continue;
            }

            if (opName == "add" && string.Equals(op.Path, "members", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<ScimGroupMember> incoming = ExtractMembers(op.Value);
                IReadOnlyList<User> incomingUsers = await LoadValidUsersAsync(incoming, ct);
                foreach (var user in incomingUsers)
                {
                    workspace.AddMember(user.Id.Value, WorkspaceRole.Member, clock.UtcNow);
                }
                continue;
            }

            if (opName == "remove" && op.Path is not null)
            {
                Match match = MemberRemovalPath().Match(op.Path);
                if (match.Success && Guid.TryParse(match.Groups["id"].Value, out Guid userGuid))
                {
                    workspace.RemoveMember(userGuid, clock.UtcNow);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<ScimGroupMember> members = await BuildMembersAsync(workspace, ct);
        return Result.Success(new ScimGroup(
            BuildGroupId(workspace.Id.Value),
            [ScimGroupSchema],
            workspace.Name.Value,
            members));
    }

    public async Task<Result> DeleteGroupAsync(
        Guid workspaceId, string groupId, CancellationToken ct = default)
    {
        if (!TryParseGroupId(groupId, out Guid groupGuid)
            || groupGuid != workspaceId)
        {
            return Result.Failure(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure(DomainError.NotFound(
                "scim.group_not_found", $"Group {groupId} was not found."));
        }

        // Off-boarding via SCIM is a soft delete (archive),
        // not a hard delete — the audit trail matters and a
        // hard delete would cascade through the workspace's
        // boards / cards / comments / votes.
        workspace.Archive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
