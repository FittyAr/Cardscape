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
public sealed partial class ScimService : IScimService
{
    private const string ScimGroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    private const string ScimListResponseSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string ScimGroupIdPrefix = "workspace-";

    private readonly IRepository<User, UserId> users;
    private readonly IUserRepository userRepository;
    private readonly IRepository<Workspace, WorkspaceId> workspaces;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    public ScimService(
        IRepository<User, UserId> users,
        IUserRepository userRepository,
        IRepository<Workspace, WorkspaceId> workspaces,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        this.users = users;
        this.userRepository = userRepository;
        this.workspaces = workspaces;
        this.unitOfWork = unitOfWork;
        this.clock = clock;
    }

    public async Task<ScimListResponse<ScimGroup>> ListGroupsAsync(
        Guid workspaceId, int startIndex, int count, CancellationToken ct = default)
    {
        // The SCIM token scopes the IdP to a single
        // workspace, so this list is always either 0 or 1
        // group. If the workspace was deleted between token
        // issuance and this call we return an empty
        // list — the IdP will reconcile.
        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return new ScimListResponse<ScimGroup>(
                [ScimListResponseSchema], 0, 0, Math.Max(1, startIndex), []);
        }

        IReadOnlyList<ScimGroupMember> members = await BuildMembersAsync(workspace, ct);
        ScimGroup group = new(
            BuildGroupId(workspace.Id.Value),
            [ScimGroupSchema],
            workspace.Name.Value,
            members);

        int pageSize = count <= 0 ? 50 : Math.Min(count, 200);
        IReadOnlyList<ScimGroup> page = [group];

        return new ScimListResponse<ScimGroup>(
            [ScimListResponseSchema],
            page.Count,
            pageSize,
            Math.Max(1, startIndex),
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

        // The minimal SCIM v2 patch surface for Groups:
        // - `replace displayName` (rename)
        // - `add` / `remove` on members
        // IdPs (Okta, Entra ID, Google Workspace) only send
        // these three shapes today; the spec is rich but
        // unused in practice.
        foreach (var op in patch.Operations)
        {
            string opName = (op.Op ?? string.Empty).ToLowerInvariant();
            if (opName != "add" && opName != "remove" && opName != "replace")
            {
                continue;
            }

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
                && (op.Path is null
                    || string.Equals(op.Path, "members", StringComparison.OrdinalIgnoreCase)))
            {
                // `replace members` with a new list is
                // treated as a full member-list replace.
                IReadOnlyList<ScimGroupMember> desired = ExtractMembers(op.Value);
                await ReplaceMembersAsync(workspace, desired, ct);
                continue;
            }

            if (opName == "add" && (op.Path is null
                || op.Path.StartsWith("members", StringComparison.OrdinalIgnoreCase)))
            {
                IReadOnlyList<ScimGroupMember> incoming = ExtractMembers(op.Value);
                IReadOnlyList<User> incomingUsers = await LoadValidUsersAsync(incoming, ct);
                foreach (var user in incomingUsers)
                {
                    workspace.AddMember(user.Id.Value, WorkspaceRole.Member, clock.UtcNow);
                }
                continue;
            }

            if (opName == "remove" && op.Path is not null
                && op.Path.StartsWith("members", StringComparison.OrdinalIgnoreCase))
            {
                // RFC 7644 paths look like
                // `members[value eq "user-guid"]` or just
                // `members`. For the bare `members` form we
                // can't infer which entry to drop, so we
                // no-op (the IdP should always send the
                // filtered form).
                int eqIdx = op.Path.IndexOf(" eq ", StringComparison.OrdinalIgnoreCase);
                if (eqIdx < 0)
                {
                    continue;
                }

                string tail = op.Path[(eqIdx + " eq ".Length)..].Trim();
                if (tail.Length >= 2 && tail[0] == '"' && tail[^1] == '"')
                {
                    tail = tail[1..^1];
                }

                if (Guid.TryParse(tail, out Guid userGuid))
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
