using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ICustomFieldsApiClient
{
    Task<ApiResult<IReadOnlyList<CustomFieldDefinitionDto>>> ListForBoardAsync(
        Guid boardId, CancellationToken ct = default);

    Task<ApiResult<CustomFieldDefinitionDto>> CreateAsync(
        Guid boardId, CreateCustomFieldRequestDto body, CancellationToken ct = default);

    Task<ApiResult<CustomFieldDefinitionDto>> RenameAsync(
        Guid boardId, Guid fieldId, RenameCustomFieldRequestDto body, CancellationToken ct = default);

    Task<ApiResult> DeleteAsync(
        Guid boardId, Guid fieldId, CancellationToken ct = default);

    Task<ApiResult<IReadOnlyList<CustomFieldValueDto>>> ListValuesForCardAsync(
        Guid cardId, CancellationToken ct = default);

    Task<ApiResult<CustomFieldValueDto>> SetValueAsync(
        Guid cardId, Guid fieldId, SetCustomFieldValueRequestDto body, CancellationToken ct = default);
}

public sealed class CustomFieldsApiClient(IHttpClientFactory http)
    : ApiClientBase(http), ICustomFieldsApiClient
{
    public async Task<ApiResult<IReadOnlyList<CustomFieldDefinitionDto>>> ListForBoardAsync(
        Guid boardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/boards/{boardId}/custom-fields/", ct);
        return await ReadAsync<IReadOnlyList<CustomFieldDefinitionDto>>(response, ct);
    }

    public async Task<ApiResult<CustomFieldDefinitionDto>> CreateAsync(
        Guid boardId, CreateCustomFieldRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/boards/{boardId}/custom-fields/", body, JsonOptions, ct);
        return await ReadAsync<CustomFieldDefinitionDto>(response, ct);
    }

    public async Task<ApiResult<CustomFieldDefinitionDto>> RenameAsync(
        Guid boardId, Guid fieldId, RenameCustomFieldRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PatchAsJsonAsync(
            $"api/boards/{boardId}/custom-fields/{fieldId}", body, JsonOptions, ct);
        return await ReadAsync<CustomFieldDefinitionDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(
        Guid boardId, Guid fieldId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/boards/{boardId}/custom-fields/{fieldId}", ct);
        return await ReadAsync(response, ct);
    }

    public async Task<ApiResult<IReadOnlyList<CustomFieldValueDto>>> ListValuesForCardAsync(
        Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/{cardId}/custom-field-values/", ct);
        return await ReadAsync<IReadOnlyList<CustomFieldValueDto>>(response, ct);
    }

    public async Task<ApiResult<CustomFieldValueDto>> SetValueAsync(
        Guid cardId, Guid fieldId, SetCustomFieldValueRequestDto body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PutAsJsonAsync(
            $"api/cards/{cardId}/custom-field-values/{fieldId}", body, ct);
        return await ReadAsync<CustomFieldValueDto>(response, ct);
    }
}
