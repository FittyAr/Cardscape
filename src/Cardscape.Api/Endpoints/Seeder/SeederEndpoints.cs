using Cardscape.Seeder;
using Cardscape.Seeder.Configuration;
using Cardscape.Seeder.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Seeder;

/// <summary>
/// REST surface for the seeder. Endpoints are feature-gated:
/// when <c>Cardscape:Seeder:Enabled</c> is <c>false</c>, every
/// route returns 404 and the admin UI is invisible. When
/// the toggle is on, the surface is:
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
/// All endpoints are <c>AllowAnonymous</c> because the
/// surface is intended for local development. A future
/// hardening pass can add the same <c>AdminOnly</c> policy
/// that protects the rest of <c>/api/admin/*</c>.
/// </summary>
public static class SeederEndpoints
{
    public static IEndpointRouteBuilder MapSeederEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/seeder")
            .WithTags("Seeder")
            .AllowAnonymous();

        group.MapGet("/status", (ISeedReportProvider provider, SeedRunner runner) =>
        {
            return Results.Ok(new
            {
                enabled = runner.IsEnabled,
                running = runner.IsRunning,
                report = ToStatus(provider.Report)
            });
        });

        group.MapGet("/options", (Microsoft.Extensions.Options.IOptions<SeederOptions> options) =>
        {
            SeederOptions snapshot = options.Value;
            return Results.Ok(new
            {
                enabled = snapshot.Enabled,
                wipeBeforeSeed = snapshot.WipeBeforeSeed,
                cardsPerBoard = snapshot.CardsPerBoard,
                userCount = snapshot.UserCount,
                fixedNow = snapshot.FixedNow
            });
        });

        // Async run: the endpoint returns 202 the moment
        // the runner accepts the request; the runner itself
        // runs on a background task. The browser polls
        // /status for the live log and the per-table
        // counts. Keeping the endpoint non-blocking means
        // a long seed (3-6 s) does not freeze the admin
        // page.
        group.MapPost("/run", async (SeedRunner runner,
                                     ISeedReportProvider provider,
                                     SeederRunRequest? request) =>
        {
            if (!runner.IsEnabled)
            {
                return Results.NotFound();
            }
            if (runner.IsRunning)
            {
                return Results.Conflict(new { error = "seeder.already_running" });
            }

            bool wipe = request?.Wipe ?? runner.CurrentOptions.WipeBeforeSeed;

            // Fire-and-forget on the runner's task scheduler.
            // The runner's own SemaphoreSlim rejects a
            // second RunAsync while the first is in flight,
            // so concurrent POST /run callers do not race.
            // Exceptions are caught inside the runner and
            // surfaced through the report (status =
            // "Failed: ...").
            //
            // We deliberately pass CancellationToken.None
            // here instead of the request's CT: as soon as
            // the browser navigates away (e.g. an auth
            // redirect that triggers Blazor's forceLoad, or
            // a curl that closed the connection), the
            // request CT fires and cancels the seed 2 ms
            // into step 1, leaving the database empty and
            // the report stuck on "Failed: The operation
            // was canceled." The runner's own internal
            // cancellation is the only meaningful signal;
            // tying it to the HTTP request lifetime is
            // the bug that produced the redirect loop on
            // /admin/seeder (no admin user -> the page
            // bounces to /login -> /login returns the
            // returnUrl -> /admin/seeder -> still no admin
            // -> /login -> ...).
            _ = Task.Run(() => _ = runner.RunAsync(wipe, CancellationToken.None));

            return Results.Accepted(value: new
            {
                running = true,
                wipe,
                startedAt = provider.Report.StartedAt
            });
        });

        group.MapPost("/wipe", async (SeedRunner runner) =>
        {
            if (!runner.IsEnabled)
            {
                return Results.NotFound();
            }
            if (runner.IsRunning)
            {
                return Results.Conflict(new { error = "seeder.already_running" });
            }

            // Same reason as /run: don't tie the wipe to
            // the request's CT or the operator gets a 2 ms
            // wipe followed by a "Failed: canceled" report.
            _ = Task.Run(() => _ = runner.WipeAsync(CancellationToken.None));

            return Results.Accepted(value: new
            {
                running = true,
                wipeOnly = true,
                startedAt = runner.CurrentOptions.FixedNow
            });
        });

        return app;
    }

    private static object ToStatus(SeedReport r) => new
    {
        status = r.Status,
        startedAt = r.StartedAt,
        finishedAt = r.FinishedAt,
        elapsed = r.Elapsed,
        currentStep = r.CurrentStep,
        totalSteps = r.TotalSteps,
        currentStepName = r.CurrentStepName,
        entries = r.Entries
            .Select(e => new
            {
                at = e.At,
                level = e.Level.ToString(),
                step = e.Step,
                message = e.Message
            })
            .ToList(),
        tables = r.TableSnapshot()
            .Select(t => new
            {
                key = t.Table,
                aggregate = t.AggregateName,
                rows = t.RowCount,
                highlight = t.Highlight
            })
            .ToList()
    };
}

/// <summary>Body for <c>POST /api/admin/seeder/run</c>.</summary>
public sealed record SeederRunRequest(bool? Wipe);
