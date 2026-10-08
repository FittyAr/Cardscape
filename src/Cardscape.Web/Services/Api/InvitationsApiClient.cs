using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IInvitationsApiClient
{
    Task<ApiResult<IReadOnlyList<WorkspaceInvitationDto>>> ListPendingAsync(CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> AcceptAsync(string token, CancellationToken ct = default);
    Task<ApiResult<WorkspaceDto>> AcceptByIdAsync(Guid invitationId, CancellationToken ct = default);
    Task<ApiResult<WorkspaceInvitationPreviewDto>> PreviewAsync(string token, CancellationToken ct = default);

    /// <summary>The link an invitee opens to accept: <c>{base}/invitations/accept?token=…</c>.</summary>
    string BuildAcceptLink(string baseUri, string token);

    Task<ApiResult<IReadOnlyList<WorkspaceInvitationDto>>> ListForWorkspaceAsync(
        Guid workspaceId, bool includeTerminal, CancellationToken ct = default);

    Task<ApiResult<WorkspaceInvitationIssuanceDto>> IssueAsync(
        Guid workspaceId, string email, WorkspaceRole role, TimeSpan? lifetime, CancellationToken ct = default);

    Task<ApiResult> RevokeAsync(Guid workspaceId, Guid invitationId, CancellationToken ct = default);
}

public sealed class InvitationsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IInvitationsApiClient
{
    public async Task<ApiResult<IReadOnlyList<WorkspaceInvitationDto>>> ListPendingAsync(
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/invitations/pending", ct);
        return await ReadAsync<IReadOnlyList<WorkspaceInvitationDto>>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> AcceptAsync(string token, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/invitations/accept", new AcceptWorkspaceInvitationRequestDto(token), JsonOptions, ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceDto>> AcceptByIdAsync(Guid invitationId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/invitations/{invitationId}/accept", content: null, ct);
        return await ReadAsync<WorkspaceDto>(response, ct);
    }

    public async Task<ApiResult<WorkspaceInvitationPreviewDto>> PreviewAsync(string token, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/invitations/preview", new AcceptWorkspaceInvitationRequestDto(token), JsonOptions, ct);
        return await ReadAsync<WorkspaceInvitationPreviewDto>(response, ct);
    }

    public string BuildAcceptLink(string baseUri, string token) =>
        $"{baseUri.TrimEnd('/')}/invitations/accept?token={Uri.EscapeDataString(token)}";

    public async Task<ApiResult<IReadOnlyList<WorkspaceInvitationDto>>> ListForWorkspaceAsync(
        Guid workspaceId, bool includeTerminal, CancellationToken ct = default)
    {
        var url = $"api/workspaces/{workspaceId}/invitations/?includeTerminal={(includeTerminal ? "true" : "false")}";
        HttpResponseMessage response = await CreateClient().GetAsync(url, ct);
        return await ReadAsync<IReadOnlyList<WorkspaceInvitationDto>>(response, ct);
    }

    public async Task<ApiResult<WorkspaceInvitationIssuanceDto>> IssueAsync(
        Guid workspaceId, string email, WorkspaceRole role, TimeSpan? lifetime, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/invitations/",
            new IssueWorkspaceInvitationRequestDto(email, role, lifetime),
            JsonOptions,
            ct);
        return await ReadAsync<WorkspaceInvitationIssuanceDto>(response, ct);
    }

    public async Task<ApiResult> RevokeAsync(
        Guid workspaceId, Guid invitationId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/workspaces/{workspaceId}/invitations/{invitationId}", ct);
        return await ReadAsync(response, ct);
    }
}
