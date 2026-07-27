using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface ICommentsApiClient
{
    Task<ApiResult<IReadOnlyList<CommentDto>>> ListForCardAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CommentDto>> AddAsync(Guid cardId, string body, CancellationToken ct = default);
    Task<ApiResult> DeleteAsync(Guid commentId, CancellationToken ct = default);
}

public sealed class CommentsApiClient(IHttpClientFactory http) : ApiClientBase(http), ICommentsApiClient
{
    public async Task<ApiResult<IReadOnlyList<CommentDto>>> ListForCardAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync($"api/cards/{cardId}/comments/", ct);
        return await ReadAsync<IReadOnlyList<CommentDto>>(response, ct);
    }

    public async Task<ApiResult<CommentDto>> AddAsync(Guid cardId, string body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            $"api/cards/{cardId}/comments/",
            new AddCommentRequestDto(body),
            ct);
        return await ReadAsync<CommentDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync($"api/comments/{commentId}", ct);
        return await ReadAsync(response, ct);
    }
}
