using System.Net.Http;
using System.Net.Http.Json;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

public interface IRecurrenceApiClient
{
    Task<ApiResult<CardRecurrenceDto?>> GetAsync(Guid cardId, CancellationToken ct = default);
    Task<ApiResult<CardRecurrenceDto>> SetAsync(
        Guid cardId, int intervalDays, DateTimeOffset firstOccurrenceAt, CancellationToken ct = default);
    Task<ApiResult> DeleteAsync(Guid cardId, CancellationToken ct = default);
}

public sealed class RecurrenceApiClient(IHttpClientFactory http)
    : ApiClientBase(http), IRecurrenceApiClient
{
    public async Task<ApiResult<CardRecurrenceDto?>> GetAsync(
        Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync(
            $"api/cards/{cardId}/recurrence/", ct);

        // The server returns 404 when the card has no recurrence row
        // (and as a side-effect of the IDOR-defence branches that also
        // fold to "no recurrence"). Treat that as the documented
        // "no recurrence" signal so CardDetail can render the page
        // for every card, not just the ones that have a recurrence set.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return ApiResult<CardRecurrenceDto?>.Ok(null);
        }

        return await ReadAsync<CardRecurrenceDto?>(response, ct);
    }

    public async Task<ApiResult<CardRecurrenceDto>> SetAsync(
        Guid cardId, int intervalDays, DateTimeOffset firstOccurrenceAt,
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PutAsJsonAsync(
            $"api/cards/{cardId}/recurrence/",
            new { intervalDays, firstOccurrenceAt }, ct);
        return await ReadAsync<CardRecurrenceDto>(response, ct);
    }

    public async Task<ApiResult> DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().DeleteAsync(
            $"api/cards/{cardId}/recurrence/", ct);
        return await ReadAsync(response, ct);
    }
}
