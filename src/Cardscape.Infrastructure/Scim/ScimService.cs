using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

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
public sealed class ScimService(
    IRepository<User, UserId> users,
    IUserRepository userRepository,
    IRepository<Workspace, WorkspaceId> workspaces,
    IUnitOfWork unitOfWork,
    IClock clock) : IScimService
{
    private const string ScimUserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ScimGroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    private const string ScimListResponseSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string ScimGroupIdPrefix = "workspace-";

    public async Task<Result<ScimUserResponse>> CreateUserAsync(
        Guid workspaceId, ScimUserCreateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return Result.Failure<ScimUserResponse>(DomainError.Validation(
                "scim.user_name_required", "userName is required."));
        }

        var emailResult = EmailAddress.Create(request.UserName);
        if (emailResult.IsFailure)
        {
            return Result.Failure<ScimUserResponse>(emailResult.Error);
        }

        // Provisioned users are created as external (no
        // password) — the IdP owns their credentials.
        var userResult = User.RegisterExternal(
            UserId.New(),
            emailResult.Value,
            BuildDisplayName(request),
            clock.UtcNow);
        if (userResult.IsFailure)
        {
            return Result.Failure<ScimUserResponse>(userResult.Error);
        }

        await users.AddAsync(userResult.Value, ct);

        // Add as a workspace member (Member role by default).
        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(workspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<ScimUserResponse>(DomainError.NotFound(
                "scim.workspace_not_found",
                $"Workspace {workspaceId} was not found."));
        }

        var addResult = workspace.AddMember(
            userResult.Value.Id.Value,
            WorkspaceRole.Member,
            clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure<ScimUserResponse>(addResult.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToResponse(userResult.Value));
    }

    public async Task<Result<IReadOnlyList<ScimUserResponse>>> ListUsersAsync(
        Guid workspaceId, int startIndex, int count, string? filter, CancellationToken ct = default)
    {
        // SCIM v2 (RFC 7644 §3.4.2.4) lets the client pass
        // any positive integer for `count`; the spec
        // recommends 10 as a default and 0 for "use the
        // server default". We clamp at 200 so a misbehaving
        // IdP cannot ask for the entire users table in one
        // call (a 2B-row response would crush the API
        // process and the IdP's UI in equal measure). The
        // SCIM startIndex is one-based; values below one are
        // normalized to the first result.
        int pageSize = count <= 0 ? 50 : Math.Min(count, 200);
        string? normalizedEmail = ParseSimpleUserNameFilter(filter);
        IReadOnlyList<User> rows = await userRepository.ListWorkspaceUsersAsync(
            new WorkspaceId(workspaceId),
            normalizedEmail,
            Math.Max(0, startIndex - 1),
            pageSize,
            ct);

        IReadOnlyList<ScimUserResponse> page = rows
            .Select(ToResponse)
            .ToList();

        return Result.Success<IReadOnlyList<ScimUserResponse>>(page);
    }

    public async Task<Result<ScimUserResponse>> GetUserAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        var user = await FindWorkspaceUserAsync(workspaceId, userId, ct);
        if (user is null)
        {
            return Result.Failure<ScimUserResponse>(DomainError.NotFound(
                "scim.user_not_found", $"User {userId} was not found."));
        }
        return Result.Success(ToResponse(user));
    }

    public async Task<Result<ScimUserResponse>> ReplaceUserAsync(
        Guid workspaceId, Guid userId, ScimUserCreateRequest request, CancellationToken ct = default)
    {
        var user = await FindWorkspaceUserAsync(workspaceId, userId, ct);
        if (user is null)
        {
            return Result.Failure<ScimUserResponse>(DomainError.NotFound(
                "scim.user_not_found", $"User {userId} was not found."));
        }

        var emailResult = EmailAddress.Create(request.UserName);
        if (emailResult.IsFailure)
        {
            return Result.Failure<ScimUserResponse>(emailResult.Error);
        }

        var updateResult = user.UpdateProfile(BuildDisplayName(request), user.AvatarUrl, clock.UtcNow);
        if (updateResult.IsFailure)
        {
            return Result.Failure<ScimUserResponse>(updateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToResponse(user));
    }

    public async Task<Result<ScimUserResponse>> PatchUserAsync(
        Guid workspaceId, Guid userId, ScimPatchRequest request, CancellationToken ct = default)
    {
        var user = await FindWorkspaceUserAsync(workspaceId, userId, ct);
        if (user is null)
        {
            return Result.Failure<ScimUserResponse>(DomainError.NotFound(
                "scim.user_not_found", $"User {userId} was not found."));
        }

        // The minimal SCIM v2 patch implementation: the only
        // operations the IdP issues today are
        // `{ "op": "replace", "path": "active", "value": false }`
        // for off-boarding. The Replace / Add variants are
        // treated identically.
        foreach (var op in request.Operations)
        {
            if (!string.Equals(op.Op, "replace", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(op.Op, "add", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(op.Path, "active", StringComparison.OrdinalIgnoreCase)
                && op.Value is bool active)
            {
                if (active)
                {
                    user.Reactivate(clock.UtcNow);
                }
                else
                {
                    user.Deactivate(clock.UtcNow);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ToResponse(user));
    }

    public async Task<Result> DeleteUserAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        var user = await FindWorkspaceUserAsync(workspaceId, userId, ct);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "scim.user_not_found", $"User {userId} was not found."));
        }

        // Off-boarding via SCIM is a soft delete (deactivate),
        // not a hard delete — the audit trail matters for
        // compliance teams and a hard delete would cascade
        // through the user's boards / comments / votes.
        user.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
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

    private static string BuildGroupId(Guid workspaceGuid) => ScimGroupIdPrefix + workspaceGuid.ToString("D");

    private static bool TryParseGroupId(string groupId, out Guid workspaceId)
    {
        workspaceId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(groupId)
            || !groupId.StartsWith(ScimGroupIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(groupId[ScimGroupIdPrefix.Length..], out workspaceId);
    }

    private async Task<IReadOnlyList<ScimGroupMember>> BuildMembersAsync(
        Workspace workspace, CancellationToken ct)
    {
        IReadOnlyList<User> members = await userRepository.ListByIdsAsync(
            workspace.Members.Select(member => new UserId(member.UserId)).ToList(),
            ct);
        Dictionary<Guid, User> usersById = members.ToDictionary(user => user.Id.Value);

        return workspace.Members
            .Select(member => new ScimGroupMember(
                member.UserId.ToString("D"),
                usersById.GetValueOrDefault(member.UserId)?.DisplayName.Value))
            .ToList();
    }

    private async Task ReplaceMembersAsync(
        Workspace workspace, IReadOnlyList<ScimGroupMember> desired, CancellationToken ct)
    {
        HashSet<Guid> desiredIds = new();
        foreach (var member in desired)
        {
            if (Guid.TryParse(member.Value, out Guid userGuid))
            {
                desiredIds.Add(userGuid);
            }
        }

        // Remove anyone not in the desired set, except the
        // owner (the aggregate forbids removing them — that
        // would also break every card in the workspace).
        List<Guid> toRemove = new();
        foreach (var m in workspace.Members)
        {
            if (!desiredIds.Contains(m.UserId) && m.UserId != workspace.OwnerId)
            {
                toRemove.Add(m.UserId);
            }
        }

        foreach (var userGuid in toRemove)
        {
            workspace.RemoveMember(userGuid, clock.UtcNow);
        }

        // Add the rest. `AddMember` is a no-op on conflict
        // (it returns `AlreadyMember`), which is exactly
        // the idempotent behaviour PUT wants.
        HashSet<Guid> missingIds = desiredIds
            .Where(userGuid => !workspace.HasMember(userGuid))
            .ToHashSet();
        IReadOnlyList<User> usersToAdd = await userRepository.ListByIdsAsync(
            missingIds.Select(userGuid => new UserId(userGuid)).ToList(),
            ct);
        foreach (var user in usersToAdd)
        {
            workspace.AddMember(user.Id.Value, WorkspaceRole.Member, clock.UtcNow);
        }
    }

    private async Task<IReadOnlyList<User>> LoadValidUsersAsync(
        IReadOnlyList<ScimGroupMember> members,
        CancellationToken ct)
    {
        HashSet<UserId> ids = [];
        foreach (var member in members)
        {
            if (Guid.TryParse(member.Value, out Guid userId))
            {
                ids.Add(new UserId(userId));
            }
        }

        return await userRepository.ListByIdsAsync(ids.ToList(), ct);
    }

    private static IReadOnlyList<ScimGroupMember> ExtractMembers(object? value)
    {
        if (value is null)
        {
            return [];
        }

        // The IdP usually sends either a
        // `IReadOnlyList<ScimGroupMember>` (System.Text.Json
        // pre-binds it) or a JsonElement array. Handle
        // both.
        if (value is IReadOnlyList<ScimGroupMember> alreadyTyped)
        {
            return alreadyTyped;
        }

        if (value is System.Text.Json.JsonElement element
            && element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            List<ScimGroupMember> rows = new();
            foreach (var item in element.EnumerateArray())
            {
                string? memberValue = null;
                string? memberDisplay = null;
                if (item.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (item.TryGetProperty("value", out var v))
                    {
                        memberValue = v.ValueKind == System.Text.Json.JsonValueKind.String
                            ? v.GetString()
                            : v.GetRawText().Trim('"');
                    }
                    if (item.TryGetProperty("display", out var d)
                        && d.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        memberDisplay = d.GetString();
                    }
                }
                if (!string.IsNullOrWhiteSpace(memberValue))
                {
                    rows.Add(new ScimGroupMember(memberValue!, memberDisplay));
                }
            }
            return rows;
        }

        return [];
    }

    private static DisplayName BuildDisplayName(ScimUserCreateRequest request) =>
        string.IsNullOrWhiteSpace(request.GivenName) && string.IsNullOrWhiteSpace(request.FamilyName)
            ? DisplayName.Create(request.UserName).Value
            : DisplayName.Create($"{request.GivenName} {request.FamilyName}".Trim()).Value;

    private static ScimUserResponse ToResponse(User u) => new(
        u.Id.Value,
        ScimUserSchema,
        u.Email.Value,
        u.DisplayName.Value.Split(' ').FirstOrDefault(),
        u.DisplayName.Value.Split(' ').Skip(1).FirstOrDefault(),
        u.IsActive,
        u.CreatedAt,
        u.UpdatedAt);

    private Task<User?> FindWorkspaceUserAsync(Guid workspaceId, Guid userId, CancellationToken ct) =>
        userRepository.FindWorkspaceUserAsync(
            new WorkspaceId(workspaceId),
            new UserId(userId),
            ct);

    private static string? ParseSimpleUserNameFilter(string? filter)
    {
        // `userName eq "..."` is the only filter the IdP sends
        // for v1.1.0. Anything else is a no-op (returns the
        // full list), so a future PR can layer the full
        // SCIM v2 filter grammar.
        const string token = "userName eq \"";
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        int idx = filter.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }
        int start = idx + token.Length;
        int end = filter.IndexOf('"', start);
        if (end <= start)
        {
            return null;
        }

        return filter[start..end].Trim().ToLowerInvariant();
    }
}
