using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>
/// Fronts <c>/api/imports/kanban/{preview|apply}</c>. The upload is
/// a <c>multipart/form-data</c> body with the Kanban
/// <c>boards.json</c> under <c>file</c> and the destination
/// workspace under <c>targetWorkspaceId</c>.
/// </summary>
public interface IImportsApiClient
{
    Task<ApiResult<ImportResultDto>> ImportKanbanAsync(
        Guid targetWorkspaceId, Stream file, string fileName, bool previewOnly, CancellationToken ct = default);
}

public sealed class ImportsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IImportsApiClient
{
    public async Task<ApiResult<ImportResultDto>> ImportKanbanAsync(
        Guid targetWorkspaceId, Stream file, string fileName, bool previewOnly, CancellationToken ct = default)
    {
        using MultipartFormDataContent form = new();
        StreamContent fileContent = new(file);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(targetWorkspaceId.ToString()), "targetWorkspaceId");

        string action = previewOnly ? "preview" : "apply";
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/imports/kanban/{action}", form, ct);
        return await ReadAsync<ImportResultDto>(response, ct);
    }
}
