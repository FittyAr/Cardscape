using Cardscape.Api.Extensions;
using Cardscape.Api.Settings;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Email;
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

        // Sends a real message with the saved SMTP settings to the signed-in
        // administrator, so a green result means invitations will go out.
        group.MapPost("/test-email", async (
            ISystemSettingsService settings,
            IEmailSender emailSender,
            ICurrentUser currentUser,
            HttpContext http,
            CancellationToken ct) =>
        {
            SystemSettings instance = await settings.GetAsync(ct);
            if (!instance.Email.CanSend())
            {
                return Results.Ok(new EmailTestResult(false, "Outbound email is off or incomplete: enable it and set the host and sender, then save."));
            }

            if (string.IsNullOrWhiteSpace(currentUser.Email))
            {
                return Results.Ok(new EmailTestResult(false, "Your account has no email address to send the test to."));
            }

            string language = EmailTemplates.ResolveLanguage(
                http.Request.Query["language"].ToString(), instance.General.DefaultLanguage);
            Result sent = await emailSender.SendAsync(
                EmailTemplates.Test(currentUser.Email, language, instance.General.InstanceTitle), ct);
            return Results.Ok(sent.IsSuccess
                ? new EmailTestResult(true, currentUser.Email)
                : new EmailTestResult(false, sent.Error.Message));
        })
        .Produces<EmailTestResult>();

        return app;
    }
}
