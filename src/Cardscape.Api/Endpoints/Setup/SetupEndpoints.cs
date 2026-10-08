using Cardscape.Api.BackgroundJobs;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Setup.Commands;
using Cardscape.Application.Setup.DTOs;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Reporting;
using Wolverine;

namespace Cardscape.Api.Endpoints.Setup;

public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/setup").WithTags("Setup");

        group.MapGet("/status", async (
            IUserRepository users,
            IConfiguration config,
            CancellationToken ct) =>
        {
            bool hasUsers = await users.AnyAsync(ct);
            string provider = config["Database:Provider"] ?? "Sqlite";

            return Results.Ok(new SetupStatusResponse(
                IsInitialized: hasUsers,
                DatabaseProvider: provider));
        })
        .AllowAnonymous()
        .Produces<SetupStatusResponse>();

        group.MapPost("/initialize", async (
            InitializeSystemCommand command,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            Result<AuthResponse> result = await bus.InvokeAsync<Result<AuthResponse>>(command, ct);

            return result.IsSuccess
                ? Results.Created("/api/auth/me", result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        })
        .AllowAnonymous()
        .Produces<AuthResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // "Start with demo data": on a brand-new instance (no users yet),
        // plant the Nexora Studios demo company instead of creating an
        // administrator by hand. Anonymous for the same reason as
        // /initialize, and refused as soon as anyone exists. The seeder
        // stays enabled afterwards so the demo can be replanted from
        // System settings.
        group.MapPost("/demo", async (
            IUserRepository users,
            ISystemSettingsService settings,
            SeederOperationQueue queue,
            CancellationToken ct) =>
        {
            if (await users.AnyAsync(ct))
            {
                return ApiProblemResults.Conflict(
                    "setup.already_initialized", "The system has already been initialized.");
            }

            SystemSettings current = await settings.GetAsync(ct);
            if (!current.Seeder.Enabled)
            {
                current.Seeder.Enabled = true;
                Result<SystemSettings> saved = await settings.UpdateAsync(current, "setup", ct);
                if (saved.IsFailure)
                {
                    return DomainErrorResults.ToProblem(saved.Error);
                }
            }

            return queue.TryEnqueueRun(wipe: false)
                ? Results.Accepted("/api/setup/demo/status")
                : ApiProblemResults.Conflict("seeder.already_running", "A Seeder operation is already running.");
        })
        .AllowAnonymous()
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // Progress of that run, for the wizard. Only coarse progress and,
        // once it succeeded, the demo sign-in (the same public demo
        // account the seeder always plants).
        group.MapGet("/demo/status", (SeedReport report, SeederOperationQueue queue) =>
        {
            bool succeeded = !queue.IsBusy && report.Status == "Succeeded";
            return Results.Ok(new DemoSetupStatusResponse(
                Running: queue.IsBusy,
                Status: report.Status,
                CurrentStep: report.CurrentStep,
                TotalSteps: report.TotalSteps,
                CurrentStepName: report.CurrentStepName,
                DemoEmail: succeeded ? NexoraStudios.DemoAdminEmail : null,
                DemoPassword: succeeded ? NexoraStudios.DemoAdminPassword : null));
        })
        .AllowAnonymous()
        .Produces<DemoSetupStatusResponse>();

        return app;
    }
}

public sealed record DemoSetupStatusResponse(
    bool Running,
    string Status,
    int CurrentStep,
    int TotalSteps,
    string? CurrentStepName,
    string? DemoEmail,
    string? DemoPassword);
