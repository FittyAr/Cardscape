using System.Net.Http;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IVotingApiClient
{
    Task<ApiResult<CardVoteStateDto>> GetStateAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardVoteStateDto>> ToggleAsync(Guid cardId, CancellationToken ct = default);
}

public sealed class VotingApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IVotingApiClient
{
    public async Task<ApiResult<CardVoteStateDto>> GetStateAsync(
        Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/{cardId}/votes/", ct);
        return await ReadAsync<CardVoteStateDto>(response, ct);
    }

    public async Task<ApiResult<CardVoteStateDto>> ToggleAsync(
        Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            $"api/cards/{cardId}/votes/", content: null, ct);
        return await ReadAsync<CardVoteStateDto>(response, ct);
    }
}
