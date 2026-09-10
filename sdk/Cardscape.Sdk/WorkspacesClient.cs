using System.Net.Http;

namespace Cardscape.Sdk;

/// <summary>Provides operations for workspaces and their memberships.</summary>
public sealed class WorkspacesClient
{
    private readonly CardscapeClient _parent;

    /// <summary>Initializes a new instance of the <see cref="WorkspacesClient"/> class.</summary>
    /// <param name="parent">The client that supplies transport and serialization.</param>
    public WorkspacesClient(CardscapeClient parent) => _parent = parent;

    /// <summary>Lists the workspaces accessible to the current user.</summary>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The accessible workspaces.</returns>
    public Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<WorkspaceDto>>(new HttpRequestMessage(HttpMethod.Get, "api/workspaces/"), ct);

    /// <summary>Gets a workspace by identifier.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The requested workspace.</returns>
    public Task<WorkspaceDto> GetAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<WorkspaceDto>(new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}"), ct);

    /// <summary>Creates a workspace.</summary>
    /// <param name="body">The workspace creation values.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The created workspace.</returns>
    public Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest body, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, "api/workspaces/") { Content = _parent.CreateJsonContent(body) };
        return _parent.SendAsync<WorkspaceDto>(req, ct);
    }

    /// <summary>Changes the data-hosting region of a workspace.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="region">One of the enumeration values that specifies the new data-hosting region.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The updated workspace.</returns>
    public Task<WorkspaceDto> SetRegionAsync(Guid workspaceId, Region region, CancellationToken ct = default)
    {
        HttpRequestMessage req = new(HttpMethod.Post, $"api/workspaces/{workspaceId}/region")
        {
            Content = _parent.CreateJsonContent(new SetWorkspaceRegionRequest(region))
        };
        return _parent.SendAsync<WorkspaceDto>(req, ct);
    }

    /// <summary>Lists the members of a workspace.</summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The workspace memberships.</returns>
    public Task<IReadOnlyList<WorkspaceMemberDto>> ListMembersAsync(Guid workspaceId, CancellationToken ct = default) =>
        _parent.SendAsync<IReadOnlyList<WorkspaceMemberDto>>(
            new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}/members"), ct);
}
