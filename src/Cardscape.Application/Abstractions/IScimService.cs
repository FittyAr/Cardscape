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
/// <remarks>
/// <para>Group mapping (added for gap G3, plan §4.4): a
/// SCIM v2 <c>Group</c> is mapped 1:1 to a
/// <see cref="Workspace"/> — the natural multi-user
/// container in Cardscape. A SCIM <c>Group</c> member is
/// mapped 1:1 to a <see cref="WorkspaceMember"/>. The SCIM
/// group <c>id</c> uses the stable form
/// <c>workspace-{guid}</c> where the guid is the workspace
/// id. The IdP-presented bearer token has already been
/// resolved to a workspace by
/// <c>ScimAuthenticationHandler</c> by the time a request
/// reaches the service, so the Groups methods take a
/// <see cref="Guid"/> workspace id the same way the Users
/// methods do.</para>
///
/// <para>Because the per-workspace <c>ScimToken</c> scopes
/// the IdP to a single workspace, <c>ListGroups</c> always
/// returns exactly one group (the token's workspace), and
/// the <c>GetGroup</c> / <c>UpdateGroup</c> /
/// <c>PatchGroup</c> / <c>DeleteGroup</c> operations all
/// act on the same workspace. The <c>CreateGroup</c> POST
/// provisions a new <see cref="Workspace"/> owned by the
/// same user that owns the token's workspace; this matches
/// the "one-organisation, one-admin" mental model IdPs
/// have.</para>
/// </remarks>
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
        Guid workspaceId, Guid userId, ScimPatchRequest request, CancellationToken ct = default);

    Task<Result> DeleteUserAsync(
        Guid workspaceId, Guid userId, CancellationToken ct = default);

    /// <summary>Returns the SCIM v2 <c>ListResponse</c>
    /// envelope (RFC 7644 §3.4.2.2). With a per-workspace
    /// <c>ScimToken</c> the <see cref="ScimListResponse{T}.Resources"/>
    /// list always contains exactly one group — the
    /// workspace the token is scoped to.</summary>
    Task<ScimListResponse<ScimGroup>> ListGroupsAsync(
        Guid workspaceId, int startIndex, int count, CancellationToken ct = default);

    /// <summary>Creates a new SCIM group, which provisions a
    /// new <see cref="Workspace"/>. The new workspace is
    /// owned by the same user that owns the token's
    /// workspace.</summary>
    Task<Result<ScimGroup>> CreateGroupAsync(
        Guid workspaceId, ScimGroup group, CancellationToken ct = default);

    /// <summary>Returns the SCIM group with the given id
    /// (<c>workspace-{guid}</c>), or 404 if the id is
    /// malformed or the workspace is not the one the token
    /// is scoped to.</summary>
    Task<Result<ScimGroup>> GetGroupAsync(
        Guid workspaceId, string groupId, CancellationToken ct = default);

    /// <summary>PUT semantics: full replace. Renames the
    /// workspace to <paramref name="group"/>.DisplayName and
    /// replaces the member list to match
    /// <paramref name="group"/>.Members. The owner is
    /// preserved.</summary>
    Task<Result<ScimGroup>> UpdateGroupAsync(
        Guid workspaceId, string groupId, ScimGroup group, CancellationToken ct = default);

    /// <summary>PATCH semantics (RFC 7644 §3.5.2). Supports
    /// the operations IdPs actually send today:
    /// <c>replace displayName</c> (rename), and
    /// <c>add</c> / <c>remove</c> on
    /// <c>members</c>.</summary>
    Task<Result<ScimGroup>> PatchGroupAsync(
        Guid workspaceId, string groupId, ScimPatchRequest patch, CancellationToken ct = default);

    /// <summary>Archives the workspace. Off-boarding via
    /// SCIM is a soft delete (archive), not a hard delete —
    /// the audit trail matters and a hard delete would
    /// cascade through the workspace's boards / cards /
    /// comments / votes.</summary>
    Task<Result> DeleteGroupAsync(
        Guid workspaceId, string groupId, CancellationToken ct = default);
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

/// <summary>SCIM v2 PATCH body (RFC 7644 §3.5.2). A list of
/// <see cref="ScimPatchOperation"/> entries: each operation
/// is an <c>add</c>, <c>remove</c>, or <c>replace</c> on a
/// path. Used by both the Users and Groups PATCH
/// endpoints.</summary>
public sealed record ScimPatchRequest(
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

/// <summary>SCIM v2 <c>Group</c> resource (RFC 7643 §8.2
/// + RFC 7644 §3.3). Mapped 1:1 to a
/// <see cref="Workspace"/>: <see cref="Id"/> is the stable
/// <c>workspace-{guid}</c> form, <see cref="DisplayName"/>
/// is the workspace name, and <see cref="Members"/> are the
/// <see cref="WorkspaceMember"/> rows. <see cref="Schemas"/>
/// is always the SCIM Group core schema on the response;
/// on the input (CreateGroup / UpdateGroup) it is
/// informational and may be empty.</summary>
public sealed record ScimGroup(
    string Id,
    IReadOnlyList<string> Schemas,
    string DisplayName,
    IReadOnlyList<ScimGroupMember> Members);

/// <summary>SCIM v2 <c>Group.member</c> sub-attribute. Maps
/// 1:1 to a <see cref="WorkspaceMember"/>: <see cref="Value"/>
/// is the user id and <see cref="Display"/> is the user's
/// display name.</summary>
public sealed record ScimGroupMember(
    string Value,
    string? Display);

/// <summary>SCIM v2 <c>ListResponse</c> envelope (RFC 7644
/// §3.4.2.2). Used by <c>GET /scim/v2/Groups</c>; the
/// <c>TotalResults</c> + <c>ItemsPerPage</c> +
/// <c>StartIndex</c> fields mirror the SCIM v2 wire
/// shape. <see cref="Schemas"/> is always the
/// <c>ListResponse</c> schema.</summary>
public sealed record ScimListResponse<T>(
    IReadOnlyList<string> Schemas,
    int TotalResults,
    int ItemsPerPage,
    int StartIndex,
    IReadOnlyList<T> Resources);
