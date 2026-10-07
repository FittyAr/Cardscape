using Cardscape.Api.BackgroundJobs;
using Cardscape.Api.Extensions;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Seeder;
using Cardscape.Seeder.Configuration;
using Cardscape.Seeder.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Seeder;

/// <summary>
/// REST surface for the seeder. Endpoints are feature-gated:
/// when <c>Cardscape:Seeder:Enabled</c> is <c>false</c> and not enabled
/// via system settings, every route returns 404 and the admin UI is disabled.
/// When the toggle is on, the surface is:
/// <list type="bullet">
///   <item><c>GET /api/admin/seeder/status</c> — current run
///   state (idle / running / done), the live log entries
///   and the per-table row counts.</item>
///   <item><c>GET /api/admin/seeder/options</c> — current
///   <see cref="SeederOptions"/> for the UI.</item>
///   <item><c>POST /api/admin/seeder/run</c> — kicks off a
///   seed run in the background. Returns 202 immediately;
///   the browser polls <c>/status</c> for progress.
///   409 if a run is already in progress.</item>
///   <item><c>POST /api/admin/seeder/wipe</c> — wipes every
///   row the seeder owns (no seed follow-up). 202 + 409
///   semantics match the run endpoint.</item>
/// </list>
/// All endpoints require the <c>AdminOnly</c> policy. The feature
/// flag controls availability; it is not an authorization boundary.
/// </summary>
public static class SeederEndpoints
{
    public static IEndpointRouteBuilder MapSeederEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/seeder")
            .WithTags("Seeder")
            .RequireAuthorization(AdminOnlyPolicy.Name);

        group.MapGet("/status", async (
            SeedReport report,
            SeederOperationQueue queue,
            ISystemSettingsService settingsService,
            CancellationToken ct) =>
        {
            SeederSettings seeder = await SeederSettingsAsync(settingsService, ct);
            return Results.Ok(new SeederStatusResponse(
                seeder.Enabled,
                queue.IsBusy,
                ToStatus(report)));
        }).Produces<SeederStatusResponse>(StatusCodes.Status200OK);

        group.MapGet("/options", async (
            Microsoft.Extensions.Options.IOptions<SeederOptions> options,
            ISystemSettingsService settingsService,
            CancellationToken ct) =>
        {
            SeederSettings seeder = await SeederSettingsAsync(settingsService, ct);
            return Results.Ok(new SeederOptionsResponse(
                seeder.Enabled,
                seeder.WipeBeforeSeed,
                options.Value.FixedNow));
        }).Produces<SeederOptionsResponse>(StatusCodes.Status200OK);

        // Async run: the endpoint returns 202 the moment
        // the runner accepts the request; the runner itself
        // runs on a background task. The browser polls
        // /status for the live log and the per-table
        // counts. Keeping the endpoint non-blocking means
        // a long seed (3-6 s) does not freeze the admin
        // page.
        group.MapPost("/run", async (
            SeederOperationQueue queue,
            SeedReport report,
            ISystemSettingsService settingsService,
            SeederRunRequest? request,
            CancellationToken ct) =>
        {
            SeederSettings seeder = await SeederSettingsAsync(settingsService, ct);
            if (!seeder.Enabled)
            {
                return ApiProblemResults.NotFound(
                    "seeder.disabled",
                    "The Seeder feature is disabled.");
            }
            bool wipe = request?.Wipe ?? seeder.WipeBeforeSeed;
            if (!queue.TryEnqueueRun(wipe))
            {
                return ApiProblemResults.Conflict(
                    "seeder.already_running",
                    "A Seeder operation is already running.");
            }

            return Results.Accepted(value: new SeederRunAcceptedResponse(
                true,
                wipe,
                report.StartedAt));
        }).Produces<SeederRunAcceptedResponse>(StatusCodes.Status202Accepted);

        group.MapPost("/wipe", async (
            SeedRunner runner,
            SeederOperationQueue queue,
            ISystemSettingsService settingsService,
            CancellationToken ct) =>
        {
            if (!(await SeederSettingsAsync(settingsService, ct)).Enabled)
            {
                return ApiProblemResults.NotFound(
                    "seeder.disabled",
                    "The Seeder feature is disabled.");
            }
            if (!queue.TryEnqueueWipe())
            {
                return ApiProblemResults.Conflict(
                    "seeder.already_running",
                    "A Seeder operation is already running.");
            }

            return Results.Accepted(value: new SeederWipeAcceptedResponse(
                true,
                true,
                runner.CurrentOptions.FixedNow));
        }).Produces<SeederWipeAcceptedResponse>(StatusCodes.Status202Accepted);

        return app;
    }

    /// <summary>The admin settings own the switch; their defaults come from <c>Cardscape:Seeder</c>.</summary>
    private static async Task<SeederSettings> SeederSettingsAsync(ISystemSettingsService settingsService, CancellationToken ct) =>
        (await settingsService.GetAsync(ct)).Seeder;

    private static SeedReportResponse ToStatus(SeedReport report) => new(
        report.Status,
        report.StartedAt,
        report.FinishedAt,
        report.Elapsed,
        report.CurrentStep,
        report.TotalSteps,
        report.CurrentStepName,
        report.Entries.Select(entry => new SeedLogEntryResponse(
            entry.At, entry.Level.ToString(), entry.Step, entry.Message)).ToList(),
        report.TableSnapshot().Select(table => new SeedTableResponse(
            table.Table, table.AggregateName, table.RowCount, table.Highlight)).ToList());
}

/// <summary>Body for <c>POST /api/admin/seeder/run</c>.</summary>
public sealed record SeederRunRequest(bool? Wipe);

public sealed record SeederStatusResponse(bool Enabled, bool Running, SeedReportResponse Report);
public sealed record SeederOptionsResponse(bool Enabled, bool WipeBeforeSeed, DateTimeOffset? FixedNow);
public sealed record SeederRunAcceptedResponse(bool Running, bool Wipe, DateTimeOffset? StartedAt);
public sealed record SeederWipeAcceptedResponse(bool Running, bool WipeOnly, DateTimeOffset? StartedAt);
public sealed record SeedReportResponse(
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    TimeSpan? Elapsed,
    int CurrentStep,
    int TotalSteps,
    string? CurrentStepName,
    IReadOnlyList<SeedLogEntryResponse> Entries,
    IReadOnlyList<SeedTableResponse> Tables);
public sealed record SeedLogEntryResponse(DateTimeOffset At, string Level, string Step, string Message);
public sealed record SeedTableResponse(string Key, string Aggregate, long Rows, string? Highlight);
