using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IGoogleDriveApiClient
{
    Task<ApiResult<string>> GetPickerUrlAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ApiResult<GoogleDriveConnectionDto>> ConnectAsync(
        Guid workspaceId, string googleEmail, string encryptedRefreshToken, CancellationToken ct = default);
    Task<ApiResult<Guid>> AttachAsync(
        Guid cardId, string fileId, string? fileName, CancellationToken ct = default);
}

public sealed class GoogleDriveApiClient(IHttpClientFactory http) : ApiClientBase(http), IGoogleDriveApiClient
{
    public async Task<ApiResult<string>> GetPickerUrlAsync(Guid workspaceId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/integrations/google/connect?workspaceId={workspaceId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return ApiResult<string>.Fail(error ?? $"HTTP {(int)response.StatusCode}");
        }

        GoogleDrivePickerUrlDto? payload =
            await response.Content.ReadFromJsonAsync<GoogleDrivePickerUrlDto>(ct);
        return payload is null
            ? ApiResult<string>.Fail("Empty response from server.")
            : ApiResult<string>.Ok(payload.PickerUrl);
    }

    public async Task<ApiResult<GoogleDriveConnectionDto>> ConnectAsync(
        Guid workspaceId, string googleEmail, string encryptedRefreshToken, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/google/connect",
            new ConnectGoogleDriveRequest(workspaceId, googleEmail, encryptedRefreshToken), ct);
        return await ReadAsync<GoogleDriveConnectionDto>(response, ct);
    }

    public async Task<ApiResult<Guid>> AttachAsync(
        Guid cardId, string fileId, string? fileName, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/integrations/google/attach",
            new AttachGoogleDriveRequest(cardId, fileId, fileName), ct);
        return await ReadAsync<Guid>(response, ct);
    }

    public sealed record ConnectGoogleDriveRequest(
        Guid WorkspaceId, string GoogleEmail, string EncryptedRefreshToken);

    public sealed record AttachGoogleDriveRequest(Guid CardId, string FileId, string? FileName);
}

public sealed record GoogleDrivePickerUrlDto(string PickerUrl);
