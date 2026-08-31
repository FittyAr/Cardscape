using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Infrastructure.Scim;

public sealed partial class ScimService
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
        return user is null
            ? Result.Failure<ScimUserResponse>(UserNotFound(userId))
            : Result.Success(ToResponse(user));
    }

    public async Task<Result<ScimUserResponse>> ReplaceUserAsync(
        Guid workspaceId, Guid userId, ScimUserCreateRequest request, CancellationToken ct = default)
    {
        var user = await FindWorkspaceUserAsync(workspaceId, userId, ct);
        if (user is null)
        {
            return Result.Failure<ScimUserResponse>(UserNotFound(userId));
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
            return Result.Failure<ScimUserResponse>(UserNotFound(userId));
        }

        foreach (var operation in request.Operations)
        {
            bool supported = string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.Op, "add", StringComparison.OrdinalIgnoreCase);
            if (!supported
                || !string.Equals(operation.Path, "active", StringComparison.OrdinalIgnoreCase)
                || operation.Value is not bool active)
            {
                continue;
            }

            if (active)
            {
                user.Reactivate(clock.UtcNow);
            }
            else
            {
                user.Deactivate(clock.UtcNow);
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
            return Result.Failure(UserNotFound(userId));
        }

        user.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private Task<User?> FindWorkspaceUserAsync(Guid workspaceId, Guid userId, CancellationToken ct) =>
        userRepository.FindWorkspaceUserAsync(
            new WorkspaceId(workspaceId),
            new UserId(userId),
            ct);

    private static DomainError UserNotFound(Guid userId) => DomainError.NotFound(
        "scim.user_not_found",
        $"User {userId} was not found.");

    private static DisplayName BuildDisplayName(ScimUserCreateRequest request) =>
        string.IsNullOrWhiteSpace(request.GivenName) && string.IsNullOrWhiteSpace(request.FamilyName)
            ? DisplayName.Create(request.UserName).Value
            : DisplayName.Create($"{request.GivenName} {request.FamilyName}".Trim()).Value;

    private static ScimUserResponse ToResponse(User user) => new(
        user.Id.Value,
        ScimUserSchema,
        user.Email.Value,
        user.DisplayName.Value.Split(' ').FirstOrDefault(),
        user.DisplayName.Value.Split(' ').Skip(1).FirstOrDefault(),
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt);

    private static string? ParseSimpleUserNameFilter(string? filter)
    {
        const string token = "userName eq \"";
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        int index = filter.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        int start = index + token.Length;
        int end = filter.IndexOf('"', start);
        return end <= start
            ? null
            : filter[start..end].Trim().ToLowerInvariant();
    }
}
