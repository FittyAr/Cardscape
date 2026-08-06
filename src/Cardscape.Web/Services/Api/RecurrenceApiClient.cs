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

        // BETA-6-#3 — the server now returns 204 No Content when
        // the card has no recurrence row (see RecurrenceEndpoints).
        // Both 204 and 404 are treated as the "no recurrence"
        // signal so the page renders for every card, not just
        // the ones with a recurrence set.
        if (response.StatusCode is System.Net.HttpStatusCode.NoContent
            or System.Net.HttpStatusCode.NotFound)
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
