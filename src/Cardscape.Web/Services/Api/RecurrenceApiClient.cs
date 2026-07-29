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
