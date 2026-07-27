using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IListsApiClient
{
    Task<ApiResult<IReadOnlyList<BoardListDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, CancellationToken ct = default);
    Task<ApiResult<BoardListDto>> CreateAsync(
        Guid boardId, string name, CancellationToken ct = default);
}

public sealed class ListsApiClient(IHttpClientFactory http) : ApiClientBase(http), IListsApiClient
{
    public async Task<ApiResult<IReadOnlyList<BoardListDto>>> ListForBoardAsync(
        Guid boardId, bool includeArchived = false, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/lists/?boardId={boardId}&includeArchived={includeArchived}", ct);
        return await ReadAsync<IReadOnlyList<BoardListDto>>(response, ct);
    }

    public async Task<ApiResult<BoardListDto>> CreateAsync(
        Guid boardId, string name, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/lists/",
            new CreateListRequestDto(boardId, name),
            ct);
        return await ReadAsync<BoardListDto>(response, ct);
    }
}
