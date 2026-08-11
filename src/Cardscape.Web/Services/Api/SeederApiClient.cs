using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>JSON shape returned by <c>GET /api/admin/seeder/status</c>.</summary>
public sealed record SeederStatusDto(
    bool Enabled,
    bool Running,
    SeederReportDto Report);

public sealed record SeederReportDto(
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    TimeSpan? Elapsed,
    int CurrentStep,
    int TotalSteps,
    string? CurrentStepName,
    IReadOnlyList<SeederLogEntryDto> Entries,
    IReadOnlyList<SeederTableStatusDto> Tables);

public sealed record SeederLogEntryDto(
    DateTimeOffset At,
    string Level,
    string Step,
    string Message);

public sealed record SeederTableStatusDto(
    string Key,
    string Aggregate,
    long Rows,
    string? Highlight);

/// <summary>JSON shape returned by <c>GET /api/admin/seeder/options</c>.</summary>
public sealed record SeederOptionsDto(
    bool Enabled,
    bool WipeBeforeSeed,
    int CardsPerBoard,
    int UserCount,
    DateTimeOffset? FixedNow);

/// <summary>JSON shape returned by <c>POST /api/admin/seeder/run</c>.</summary>
public sealed record SeederRunAcceptedDto(
    bool Running,
    bool Wipe,
    DateTimeOffset? StartedAt);

public sealed record SeederWipeAcceptedDto(
    bool Running,
    bool WipeOnly,
    DateTimeOffset? StartedAt);

public interface ISeederApiClient
{
    Task<ApiResult<SeederStatusDto>> GetStatusAsync(CancellationToken ct = default);
    Task<ApiResult<SeederOptionsDto>> GetOptionsAsync(CancellationToken ct = default);
    Task<ApiResult<SeederRunAcceptedDto>> RunAsync(bool wipe, CancellationToken ct = default);
    Task<ApiResult<SeederWipeAcceptedDto>> WipeAsync(CancellationToken ct = default);
}

/// <summary>
/// Thin client over the four <c>/api/admin/seeder/*</c>
/// endpoints. The /run and /wipe endpoints return 202
/// with a "running" acknowledgement; the page polls
/// /status to surface the live log and the per-table
/// row counts.
/// </summary>
public sealed class SeederApiClient(IHttpClientFactory http) : ApiClientBase(http), ISeederApiClient
{
    public async Task<ApiResult<SeederStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/admin/seeder/status", ct);
        return await ReadAsync<SeederStatusDto>(response, ct);
    }

    public async Task<ApiResult<SeederOptionsDto>> GetOptionsAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().GetAsync("api/admin/seeder/options", ct);
        return await ReadAsync<SeederOptionsDto>(response, ct);
    }

    public async Task<ApiResult<SeederRunAcceptedDto>> RunAsync(bool wipe, CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            "api/admin/seeder/run", new { wipe }, JsonOptions, ct);
        return await ReadAsync<SeederRunAcceptedDto>(response, ct);
    }

    public async Task<ApiResult<SeederWipeAcceptedDto>> WipeAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response = await CreateClient().PostAsync(
            "api/admin/seeder/wipe", content: null, ct);
        return await ReadAsync<SeederWipeAcceptedDto>(response, ct);
    }
}
