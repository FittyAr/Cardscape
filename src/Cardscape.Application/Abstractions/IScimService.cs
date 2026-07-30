using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions;

/// <summary>
/// SCIM v2 (RFC 7644) user + group provisioning. The IdP
/// (Okta, Azure AD, Google Workspace, etc.) talks to
/// <c>/scim/v2/Users</c> + <c>/scim/v2/Groups</c>; the
/// implementation bridges the SCIM shape to the
/// <see cref="User"/> aggregate and the
/// <see cref="WorkspaceMember"/> assignment.
/// </summary>
public interface IScimService
{
    Task<Result<ScimUserResponse>> CreateUserAsync(
        Guid workspaceId, ScimUserCreateRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ScimUserResponse>>> ListUsersAsync(
        Guid workspaceId, int startIndex, int count, string? filter, CancellationToken ct = default);

    Task<Result<ScimUserResponse>> GetUserAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default);

    Task<Result<ScimUserResponse>> ReplaceUserAsync(
        Guid workspaceId, Guid userId, ScimUserCreateRequest request, CancellationToken ct = default);

    Task<Result<ScimUserResponse>> PatchUserAsync(
        Guid workspaceId, Guid userId, ScimUserPatchRequest request, CancellationToken ct = default);

    Task<Result> DeleteUserAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default);
}

/// <summary>Subset of SCIM v2 <c>User</c> — only the fields
/// Cardscape reads. <c>userName</c> is the email; <c>active</c>
/// maps to <see cref="User.IsActive"/>; <c>name.givenName</c>
/// and <c>name.familyName</c> compose the display name.</summary>
public sealed record ScimUserCreateRequest(
    string UserName,
    string? GivenName,
    string? FamilyName,
    bool Active,
    string? Password);

public sealed record ScimUserPatchRequest(
    IReadOnlyList<ScimPatchOperation> Operations);

public sealed record ScimPatchOperation(string Op, string? Path, object? Value);

/// <summary>SCIM v2-shaped user response (the
/// <c>schemas</c> + <c>meta</c> + flat <c>User</c> shape the
/// IdPs expect). <c>Id</c> is the Cardscape
/// <c>User.Id</c>; the IdP stores it as the
/// <c>externalId</c> on its side.</summary>
public sealed record ScimUserResponse(
    Guid Id,
    string Schemas,
    string UserName,
    string? GivenName,
    string? FamilyName,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastModifiedAt);
