using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Integrations.GoogleCalendar;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
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
/// + <c>oauth2.googleapis.com/token</c>). Only the working
/// outbound calendar synchronization is supported.
/// </summary>
public static class GoogleCalendarOAuthEndpoints
{
    private const string StatePurpose = "Cardscape.GoogleCalendar.OAuthState.v1";
    private const int MaxGoogleResponseBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapGoogleCalendarOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var oauthGroup = app.MapGroup("/api/integrations/google-calendar")
            .WithTags("Integrations.GoogleCalendar");

        oauthGroup.MapGet("/start", async (
            HttpContext http,
            IConfiguration configuration,
            IDataProtectionProvider dataProtection,
            IMessageBus bus,
            [FromQuery] Guid workspaceId,
            [FromQuery] string? returnUrl,
            CancellationToken ct) =>
        {
            if (workspaceId == Guid.Empty)
            {
                return Results.Problem(
                    title: "google_calendar.workspace_required",
                    detail: "workspaceId query parameter is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            Result<GoogleCalendarOAuthAuthorization> authorization = await bus.InvokeAsync<
                Result<GoogleCalendarOAuthAuthorization>>(
                new AuthorizeGoogleCalendarOAuthQuery(workspaceId), ct);
            if (authorization.IsFailure)
            {
                return MapError(authorization.Error);
            }

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

            string localReturnUrl = IsLocalReturnUrl(returnUrl)
                ? returnUrl!
                : "/settings/integrations/google-calendar?connected=1";
            var statePayload = new GoogleCalendarOAuthState(
                authorization.Value.UserId,
                authorization.Value.WorkspaceId,
                localReturnUrl);
            ITimeLimitedDataProtector protector = dataProtection
                .CreateProtector(StatePurpose)
                .ToTimeLimitedDataProtector();
            string state = protector.Protect(
                JsonSerializer.Serialize(statePayload), TimeSpan.FromMinutes(10));

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
        }).RequireAuthorization();

        oauthGroup.MapGet("/callback", async (
            HttpContext http,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ISecretProtector secrets,
            IDataProtectionProvider dataProtection,
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

            GoogleCalendarOAuthState state;
            try
            {
                ITimeLimitedDataProtector protector = dataProtection
                    .CreateProtector(StatePurpose)
                    .ToTimeLimitedDataProtector();
                string serialized = protector.Unprotect(stateValues.ToString());
                state = JsonSerializer.Deserialize<GoogleCalendarOAuthState>(serialized)
                    ?? throw new CryptographicException("OAuth state payload is empty.");
            }
            catch (CryptographicException)
            {
                return Results.Problem(
                    title: "google_calendar.state_invalid",
                    detail: "The OAuth state is invalid or expired. Restart the connection flow.",
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
            using var tokenRequest = new HttpRequestMessage(
                HttpMethod.Post, "https://oauth2.googleapis.com/token")
            { Content = content };
            using HttpResponseMessage tokenResponse = await tokenHttp.SendAsync(
                tokenRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return Results.Problem(
                    title: "google_calendar.token_exchange_failed",
                    detail: $"Google token exchange failed with status {(int)tokenResponse.StatusCode}.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            Result<JsonElement> tokenBodyResult = await ReadGoogleJsonAsync(tokenResponse.Content, ct);
            if (tokenBodyResult.IsFailure)
            {
                return MapError(tokenBodyResult.Error);
            }

            JsonElement tokenBody = tokenBodyResult.Value;
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
                using var userRequest = new HttpRequestMessage(
                    HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
                userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using HttpResponseMessage userResponse = await userHttp.SendAsync(
                    userRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                if (userResponse.IsSuccessStatusCode)
                {
                    Result<JsonElement> userBodyResult = await ReadGoogleJsonAsync(userResponse.Content, ct);
                    if (userBodyResult.IsFailure)
                    {
                        return MapError(userBodyResult.Error);
                    }

                    JsonElement userBody = userBodyResult.Value;
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

            var result = await bus.InvokeAsync<Result<GoogleCalendarConnectionDto>>(
                new CompleteGoogleCalendarOAuthCommand(
                    state.UserId,
                    state.WorkspaceId,
                    googleEmail,
                    encrypted,
                    "primary"),
                ct);

            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            return Results.Redirect(state.ReturnUrl);
        }).AllowAnonymous();

        return app;
    }

    private static bool IsLocalReturnUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value[0] == '/'
        && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'));

    private static async Task<Result<JsonElement>> ReadGoogleJsonAsync(
        HttpContent content,
        CancellationToken ct)
    {
        if (content.Headers.ContentLength is long length && length > MaxGoogleResponseBytes)
        {
            return Result.Failure<JsonElement>(DomainError.External(
                "google_calendar.response_too_large",
                $"Google response exceeded {MaxGoogleResponseBytes} bytes."));
        }

        try
        {
            await content.LoadIntoBufferAsync(MaxGoogleResponseBytes, ct);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<JsonElement>(DomainError.External(
                "google_calendar.response_too_large",
                $"Google response exceeded {MaxGoogleResponseBytes} bytes."));
        }

        try
        {
            JsonElement body = await content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return Result.Success(body);
        }
        catch (JsonException)
        {
            return Result.Failure<JsonElement>(DomainError.External(
                "google_calendar.invalid_response",
                "Google returned invalid JSON."));
        }
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

internal sealed record GoogleCalendarOAuthState(Guid UserId, Guid WorkspaceId, string ReturnUrl);
