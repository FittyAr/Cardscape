using Cardscape.Api.Extensions;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Cardscape.Api.Middleware;

/// <summary>
/// While maintenance mode is on, answers every API request with 503 and the
/// administrator's message — except for administrators (so they can finish
/// the work and switch it off) and the endpoints needed to sign in, read the
/// public instance settings and run health checks.
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AlwaysOpen =
    [
        "/api/auth",
        "/api/setup",
        "/api/admin",
        "/api/internal",
        "/health",
    ];

    public async Task InvokeAsync(HttpContext context, ISystemSettingsService settings, IAuthorizationService authorization)
    {
        if (!IsGuardedApiCall(context))
        {
            await next(context);
            return;
        }

        NoticeSettings notices = (await settings.GetAsync(context.RequestAborted)).Notices;
        if (!notices.MaintenanceEnabled
            || (await authorization.AuthorizeAsync(context.User, AdminOnlyPolicy.Name)).Succeeded)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "300";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.6.4",
            title = "Under maintenance",
            status = StatusCodes.Status503ServiceUnavailable,
            detail = notices.MaintenanceMessage ?? "The service is under maintenance.",
            code = "system.maintenance",
        }, context.RequestAborted);
    }

    private static bool IsGuardedApiCall(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api")
        && !AlwaysOpen.Any(prefix => context.Request.Path.StartsWithSegments(prefix));
}
