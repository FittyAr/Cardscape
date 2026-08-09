using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>
/// BUG-A5-002 — see test-results/beta/reports/A5-card-extras.md.
/// The card attachments surface used to exist only on the
/// domain side; this client fronts the new
/// <c>/api/cards/{id}/attachments/...</c> endpoints so the
/// CardDetail.razor page can list, upload, download and delete
/// files. The download path streams the bytes through the
/// HttpClient; the upload path sends a <c>multipart/form-data</c>
/// body with the file under the <c>file</c> field.
/// </summary>
public interface IAttachmentsApiClient
{
    Task<ApiResult<IReadOnlyList<AttachmentDto>>> ListAsync(
        Guid cardId, CancellationToken ct = default);

    Task<ApiResult<AttachmentDto>> UploadAsync(
        Guid cardId, Stream file, string fileName, string contentType, CancellationToken ct = default);

    Task<ApiResult<byte[]>> DownloadAsync(
        Guid cardId, Guid attachmentId, CancellationToken ct = default);

    Task<ApiResult<bool>> DeleteAsync(
        Guid cardId, Guid attachmentId, CancellationToken ct = default);
}

public sealed class AttachmentsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IAttachmentsApiClient
{
    public async Task<ApiResult<IReadOnlyList<AttachmentDto>>> ListAsync(
        Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/{cardId}/attachments/", ct);
        return await ReadAsync<IReadOnlyList<AttachmentDto>>(response, ct);
    }

    public async Task<ApiResult<AttachmentDto>> UploadAsync(
        Guid cardId, Stream file, string fileName, string contentType, CancellationToken ct = default)
    {
        using MultipartFormDataContent form = new();
        StreamContent fileContent = new(file);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/cards/{cardId}/attachments/", form, ct);
        return await ReadAsync<AttachmentDto>(response, ct);
    }

    public async Task<ApiResult<byte[]>> DownloadAsync(
        Guid cardId, Guid attachmentId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/{cardId}/attachments/{attachmentId}/download", ct);
        if (!response.IsSuccessStatusCode)
        {
            string? error = await AuthService.ExtractErrorAsync(response, ct);
            return ApiResult<byte[]>.Fail(error ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return ApiResult<byte[]>.Ok(bytes);
    }

    public async Task<ApiResult<bool>> DeleteAsync(
        Guid cardId, Guid attachmentId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/cards/{cardId}/attachments/{attachmentId}", ct);
        if (response.IsSuccessStatusCode)
        {
            return ApiResult<bool>.Ok(true);
        }
        string? err = await AuthService.ExtractErrorAsync(response, ct);
        return ApiResult<bool>.Fail(err ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
    }
}
