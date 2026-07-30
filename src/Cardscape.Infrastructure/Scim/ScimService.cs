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
        IReadOnlyList<WorkspaceMember> members = await userRepository
            .ListWorkspaceMembersAsync(new WorkspaceId(workspaceId), ct);

        List<User> rows = new();
        foreach (var m in members)
        {
            var u = await users.GetByIdAsync(new UserId(m.UserId), ct);
            if (u is not null)
            {
                rows.Add(u);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            // SCIM v2 filters of the form
            // `userName eq "alice@example.com"`.
            // For v1.1.0 the parser is intentionally minimal.
            rows = ApplySimpleUserNameFilter(rows, filter).ToList();
        }

        IReadOnlyList<ScimUserResponse> page = rows
            .Skip(Math.Max(0, startIndex))
            .Take(count <= 0 ? 50 : count)
            .Select(ToResponse)
            .ToList();

        return Result.Success<IReadOnlyList<ScimUserResponse>>(page);
    }

    public async Task<Result<ScimUserResponse>> GetUserAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), ct);
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
        var user = await users.GetByIdAsync(new UserId(userId), ct);
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
        Guid workspaceId, Guid userId, ScimUserPatchRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), ct);
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
        var user = await users.GetByIdAsync(new UserId(userId), ct);
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

    private static IEnumerable<User> ApplySimpleUserNameFilter(IEnumerable<User> users, string filter)
    {
        // `userName eq "..."` is the only filter the IdP sends
        // for v1.1.0. Anything else is a no-op (returns the
        // full list), so a future PR can layer the full
        // SCIM v2 filter grammar.
        const string token = "userName eq \"";
        int idx = filter.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return users;
        }
        int start = idx + token.Length;
        int end = filter.IndexOf('"', start);
        if (end <= start)
        {
            return users;
        }
        string email = filter[start..end];
        return users.Where(u => string.Equals(u.Email.Value, email, StringComparison.OrdinalIgnoreCase));
    }
}
