using System.Net.Http;
using System.Net.Http.Json;

namespace Cardscape.Web.Services.Api;

public interface IAiApiClient
{
    Task<ApiResult<AiGeneratedTextDto>> GenerateDescriptionAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<AiGeneratedChecklistDto>> GenerateChecklistAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<AiOwnerSuggestionsDto>> SuggestOwnersAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<AiGeneratedTextDto>> SummarizeCommentsAsync(IReadOnlyList<Guid> commentIds, CancellationToken ct = default);
}

public sealed class AiApiClient(IHttpClientFactory httpClientFactory)
    : ApiClientBase(httpClientFactory), IAiApiClient
{
    public async Task<ApiResult<AiGeneratedTextDto>> GenerateDescriptionAsync(Guid cardId, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PostAsync(
                $"api/ai/cards/{cardId}/generate-description", content: null, ct);
            return await ReadAsync<AiGeneratedTextDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<AiGeneratedTextDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<AiGeneratedChecklistDto>> GenerateChecklistAsync(Guid cardId, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PostAsync(
                $"api/ai/cards/{cardId}/generate-checklist", content: null, ct);
            return await ReadAsync<AiGeneratedChecklistDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<AiGeneratedChecklistDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<AiOwnerSuggestionsDto>> SuggestOwnersAsync(Guid cardId, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PostAsync(
                $"api/ai/cards/{cardId}/suggest-owners", content: null, ct);
            return await ReadAsync<AiOwnerSuggestionsDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<AiOwnerSuggestionsDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<AiGeneratedTextDto>> SummarizeCommentsAsync(
        IReadOnlyList<Guid> commentIds, CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = CreateClient();
            using HttpResponseMessage response = await http.PostAsJsonAsync(
                "api/ai/comments/summarize", new { commentIds }, ct);
            return await ReadAsync<AiGeneratedTextDto>(response, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<AiGeneratedTextDto>.Fail(ex.Message);
        }
    }
}

public sealed record AiGeneratedTextDto(string Text, string Model);

public sealed record AiGeneratedChecklistDto(IReadOnlyList<string> Items, string Model);

public sealed record AiOwnerSuggestionDto(Guid UserId, string DisplayName, string Reason);

public sealed record AiOwnerSuggestionsDto(IReadOnlyList<AiOwnerSuggestionDto> Suggestions, string Model);
