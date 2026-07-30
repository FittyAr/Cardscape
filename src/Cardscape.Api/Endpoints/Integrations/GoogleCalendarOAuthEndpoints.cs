using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Integrations.GoogleCalendar;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Cardscape.Api.Endpoints.Integrations;

/// <summary>
/// Google Calendar OAuth start/callback + webhook receiver
/// endpoints. Kept in a separate file from
/// <see cref="GoogleCalendarEndpoints"/> because the OAuth
/// round-trip is a different transport (anonymous GETs
/// against Google's <c>accounts.google.com/o/oauth2/v2/auth</c>
/// + <c>oauth2.googleapis.com/token</c>) and the webhook is
/// a server-to-server POST (no JWT, just channel + resource
/// id headers).
/// </summary>
public static class GoogleCalendarOAuthEndpoints
{
    public const string StateCookieName = "cardscape.gcal.state";
    public const string StateWorkspaceCookieName = "cardscape.gcal.workspace";

    public static IEndpointRouteBuilder MapGoogleCalendarOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var oauthGroup = app.MapGroup("/api/integrations/google-calendar")
            .WithTags("Integrations.GoogleCalendar");

        oauthGroup.MapGet("/start", (
            HttpContext http,
            IConfiguration configuration,
            [FromQuery] Guid workspaceId,
            [FromQuery] string? returnUrl) =>
        {
            string clientId = configuration["Integrations:GoogleCalendar:ClientId"] ?? string.Empty;
            string redirectUri = configuration["Integrations:GoogleCalendar:RedirectUri"]
                ?? $"{http.Request.Scheme}://{http.Request.Host}/api/integrations/google-calendar/callback";

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.Problem(
                    title: "google_calendar.not_configured",
                    detail: "Google Calendar integration is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (workspaceId == Guid.Empty)
            {
                return Results.Problem(
                    title: "google_calendar.workspace_required",
                    detail: "workspaceId query parameter is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string state = Guid.NewGuid().ToString("N");
            http.Response.Cookies.Append(StateCookieName, state, new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });
            http.Response.Cookies.Append(StateWorkspaceCookieName, workspaceId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });

            string scope = Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.events email profile");
            string authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={Uri.EscapeDataString(clientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + $"&response_type=code"
                + $"&scope={scope}"
                + $"&state={state}"
                + $"&access_type=offline"
                + $"&prompt=consent"
                + $"&include_granted_scopes=true";
            return Results.Redirect(authUrl);
        }).AllowAnonymous();

        oauthGroup.MapGet("/callback", async (
            HttpContext http,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ISecretProtector secrets,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!http.Request.Query.TryGetValue("code", out var codeValues) || string.IsNullOrEmpty(codeValues))
            {
                return Results.Problem(
                    title: "google_calendar.missing_code",
                    detail: "Google did not return an authorization code.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!http.Request.Query.TryGetValue("state", out var stateValues)
                || string.IsNullOrEmpty(stateValues))
            {
                return Results.Problem(
                    title: "google_calendar.missing_state",
                    detail: "Google did not return a state parameter.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string? expectedState = http.Request.Cookies[StateCookieName];
            string? workspaceIdCookie = http.Request.Cookies[StateWorkspaceCookieName];
            if (string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(workspaceIdCookie))
            {
                return Results.Problem(
                    title: "google_calendar.state_expired",
                    detail: "The OAuth state cookie has expired. Restart the connection flow.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!string.Equals(expectedState, stateValues.ToString(), StringComparison.Ordinal))
            {
                return Results.Problem(
                    title: "google_calendar.state_mismatch",
                    detail: "The OAuth state parameter does not match the cookie.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!Guid.TryParse(workspaceIdCookie, out Guid workspaceId) || workspaceId == Guid.Empty)
            {
                return Results.Problem(
                    title: "google_calendar.workspace_required",
                    detail: "The workspace cookie is invalid. Restart the connection flow.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string clientId = configuration["Integrations:GoogleCalendar:ClientId"] ?? string.Empty;
            string clientSecret = configuration["Integrations:GoogleCalendar:ClientSecret"] ?? string.Empty;
            string redirectUri = configuration["Integrations:GoogleCalendar:RedirectUri"]
                ?? $"{http.Request.Scheme}://{http.Request.Host}/api/integrations/google-calendar/callback";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return Results.Problem(
                    title: "google_calendar.not_configured",
                    detail: "Google Calendar integration is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            using HttpClient tokenHttp = httpClientFactory.CreateClient("google-oauth");
            using FormUrlEncodedContent content = new(new Dictionary<string, string>
            {
                ["code"] = codeValues.ToString(),
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });
            HttpResponseMessage tokenResponse = await tokenHttp.PostAsync(
                "https://oauth2.googleapis.com/token", content, ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                string body = await tokenResponse.Content.ReadAsStringAsync(ct);
                return Results.Problem(
                    title: "google_calendar.token_exchange_failed",
                    detail: $"Google token exchange failed ({(int)tokenResponse.StatusCode}): {body}",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            JsonElement tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            string? refreshToken = tokenBody.TryGetProperty("refresh_token", out JsonElement rt) ? rt.GetString() : null;
            string? accessToken = tokenBody.TryGetProperty("access_token", out JsonElement at) ? at.GetString() : null;
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Problem(
                    title: "google_calendar.refresh_token_missing",
                    detail: "Google did not return a refresh token.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            string? googleEmail = null;
            if (!string.IsNullOrEmpty(accessToken))
            {
                using HttpClient userHttp = httpClientFactory.CreateClient("google-oauth");
                userHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage userResponse = await userHttp.GetAsync(
                    "https://www.googleapis.com/oauth2/v3/userinfo", ct);
                if (userResponse.IsSuccessStatusCode)
                {
                    JsonElement userBody = await userResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                    googleEmail = userBody.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
                }
            }

            if (string.IsNullOrWhiteSpace(googleEmail))
            {
                return Results.Problem(
                    title: "google_calendar.email_missing",
                    detail: "Google did not return the account email address.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            string encrypted = secrets.Protect(refreshToken);

            http.Response.Cookies.Delete(StateCookieName);
            http.Response.Cookies.Delete(StateWorkspaceCookieName);

            var result = await bus.InvokeAsync<Result<GoogleCalendarConnectionDto>>(
                new EstablishGoogleCalendarConnectionCommand(
                    workspaceId,
                    googleEmail,
                    encrypted,
                    "primary"),
                ct);

            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            string webRedirect = configuration["Cardscape:Web:GoogleCalendarRedirectUrl"]
                ?? configuration["Web:GoogleCalendarRedirectUrl"]
                ?? "/settings/integrations/google-calendar?connected=1";
            return Results.Redirect(webRedirect);
        }).AllowAnonymous();

        oauthGroup.MapPost("/watch", async (
            HttpContext http,
            IConfiguration configuration,
            IGoogleCalendarConnectionRepository connections,
            IGoogleCalendarSyncService sync,
            IClock clock,
            CancellationToken ct) =>
        {
            string? userIdValue = http.User.FindFirst("sub")?.Value
                ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdValue) || !Guid.TryParse(userIdValue, out Guid userId))
            {
                return Results.Unauthorized();
            }

            string webhookUrl = configuration["Integrations:GoogleCalendar:WebhookUrl"]
                ?? $"{http.Request.Scheme}://{http.Request.Host}/api/integrations/google-calendar/webhook";

            Result<GoogleCalendarWatchInfo> watchResult = await sync.WatchCalendarAsync(userId, webhookUrl, ct);
            if (watchResult.IsFailure)
            {
                return MapError(watchResult.Error);
            }

            var connection = await connections.FindByUserAsync(userId, ct);
            if (connection is not null)
            {
                connection.SetWatch(
                    watchResult.Value.ChannelId,
                    watchResult.Value.ResourceId,
                    watchResult.Value.ExpiresAt,
                    clock.UtcNow);
                await connections.UpdateAsync(connection, ct);
            }

            return Results.Ok(new
            {
                channelId = watchResult.Value.ChannelId,
                resourceId = watchResult.Value.ResourceId,
                expiresAt = watchResult.Value.ExpiresAt
            });
        }).RequireAuthorization();

        oauthGroup.MapPost("/webhook", async (
            HttpContext http,
            IGoogleCalendarConnectionRepository connections,
            IGoogleCalendarSyncService sync,
            ILogger<GoogleCalendarMarker> logger,
            CancellationToken ct) =>
        {
            string? channelId = http.Request.Headers["X-Goog-Channel-Id"].ToString();
            string? resourceId = http.Request.Headers["X-Goog-Resource-Id"].ToString();
            string? resourceState = http.Request.Headers["X-Goog-Resource-State"].ToString();

            if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(resourceId))
            {
                return Results.BadRequest(new { error = "missing_channel_headers" });
            }

            if (string.Equals(resourceState, "stop", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Google Calendar watch {ChannelId} (resource {ResourceId}) reported stop.",
                    channelId, resourceId);
                return Results.Ok();
            }

            IReadOnlyList<Domain.Integrations.GoogleCalendar.GoogleCalendarConnection> matches =
                await FindByChannelAsync(connections, channelId, resourceId, ct);

            if (matches.Count == 0)
            {
                logger.LogWarning(
                    "Google Calendar webhook for channel {ChannelId} resource {ResourceId} found no matching connection.",
                    channelId, resourceId);
                return Results.NotFound();
            }

            int totalUpdated = 0;
            foreach (Domain.Integrations.GoogleCalendar.GoogleCalendarConnection connection in matches)
            {
                Result<int> pull = await sync.PullCalendarChangesAsync(connection.UserId, ct);
                if (pull.IsFailure)
                {
                    logger.LogWarning(
                        "Pull for user {UserId} on webhook failed: {Code} {Message}",
                        connection.UserId, pull.Error.Code, pull.Error.Message);
                    continue;
                }
                totalUpdated += pull.Value;
            }

            return Results.Ok(new { updated = totalUpdated });
        }).AllowAnonymous();

        return app;
    }

    private static async Task<IReadOnlyList<Domain.Integrations.GoogleCalendar.GoogleCalendarConnection>> FindByChannelAsync(
        IGoogleCalendarConnectionRepository connections,
        string channelId,
        string resourceId,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        return [];
    }

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.External => Results.Json(new { error.Code, error.Message }, statusCode: StatusCodes.Status502BadGateway),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}

internal sealed class GoogleCalendarMarker { }
