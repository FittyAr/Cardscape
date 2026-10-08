using Cardscape.Api.Extensions;
using Cardscape.Api.Settings;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;

namespace Cardscape.Api.Endpoints.Admin;

/// <summary>
/// Instance administration: the runtime-editable <see cref="SystemSettings"/>,
/// a read-only report of the startup configuration, live diagnostics and an
/// AI connectivity check. Administrators only.
/// </summary>
public static class AdminSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAdminSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/settings")
            .WithTags("AdminSettings")
            .RequireAuthorization(AdminOnlyPolicy.Name);

        group.MapGet("/", async (ISystemSettingsService settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAsync(ct)))
            .Produces<SystemSettings>();

        group.MapPut("/", async (
            SystemSettings request,
            ISystemSettingsService settings,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            Result<SystemSettings> updated = await settings.UpdateAsync(request, currentUser.Email, ct);
            return updated.ToOk();
        })
        .Produces<SystemSettings>()
        .ProducesValidationProblem();

        group.MapPost("/reset", async (ISystemSettingsService settings, ICurrentUser currentUser, CancellationToken ct) =>
            Results.Ok(await settings.ResetAsync(currentUser.Email, ct)))
            .Produces<SystemSettings>();

        group.MapGet("/runtime", (RuntimeConfigurationReport report) => Results.Ok(report.Build()))
            .Produces<RuntimeConfigurationEntry[]>();

        group.MapGet("/diagnostics", async (SystemDiagnosticsProbe probe, CancellationToken ct) =>
            Results.Ok(await probe.ProbeAsync(ct)))
            .Produces<SystemDiagnostics>();

        // Exercises the real completion path (endpoint, key, model, timeout)
        // with a one-word prompt, so a green result means the assistant works.
        group.MapPost("/test-ai", async (IAiService ai, CancellationToken ct) =>
        {
            Result<AiTextCompletion> reply = await ai.CompleteAsync(
                new AiPrompt("You are a health check. Reply with the single word OK.", "Ping"),
                new AiOptions(Temperature: 0, MaxTokens: 8),
                ct);
            return Results.Ok(reply.IsSuccess
                ? new AiConnectionTestResult(true, $"{reply.Value.Model ?? "model"}: {reply.Value.Text.Trim()}")
                : new AiConnectionTestResult(false, reply.Error.Message));
        })
        .Produces<AiConnectionTestResult>();

        return app;
    }
}
