using Cardscape.Seeder;
using Cardscape.Seeder.Configuration;
using Cardscape.Seeder.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Seeder;

/// <summary>
/// REST surface for the seeder. The endpoints are
/// feature-gated: when <c>Cardscape:Seeder:Enabled</c> is
/// <c>false</c>, the <see cref="MapSeederEndpoints"/> extension
/// registers nothing at all so the routes do not exist (the
/// SPA client sees 404s on the admin page). When the toggle
/// is on, the surface is:
/// <list type="bullet">
///   <item><c>GET /api/admin/seeder/status</c> — table row
///   counts, current run status, and the recent log
///   stream.</item>
///   <item><c>POST /api/admin/seeder/run</c> — kicks off a
///   seed run; 409 if one is already in progress.</item>
///   <item><c>POST /api/admin/seeder/wipe</c> — wipes every
///   table the seeder owns (no seed follow-up).</item>
///   <item><c>GET /api/admin/seeder/options</c> — current
///   <see cref="SeederOptions"/> for the UI.</item>
/// </list>
/// All endpoints are <c>AllowAnonymous</c> for the moment
/// because the surface is intended for local development.
/// A future hardening pass can add an admin-policy gate
/// (the same <c>AdminOnly</c> policy that protects the rest
/// of <c>/api/admin/*</c>).
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

        group.MapPost("/run", async (SeedRunner runner,
                                     ISeedReportProvider provider,
                                     SeederRunRequest? request,
                                     CancellationToken cancellationToken) =>
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
            SeedReport report = await runner.RunAsync(wipe, cancellationToken);
            return Results.Ok(ToStatus(report));
        });

        group.MapPost("/wipe", async (SeedRunner runner, CancellationToken cancellationToken) =>
        {
            if (!runner.IsEnabled)
            {
                return Results.NotFound();
            }
            if (runner.IsRunning)
            {
                return Results.Conflict(new { error = "seeder.already_running" });
            }

            SeedReport report = await runner.WipeAsync(cancellationToken);
            return Results.Ok(ToStatus(report));
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
