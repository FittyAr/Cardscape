using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cardscape.Api.Middleware;

/// <summary>
/// Per-API-token rate limiting. Reads the
/// <see cref="ApiToken"/> that the auth pipeline deposits in
/// <c>HttpContext.Items["ApiToken"]</c>, asks the
/// <see cref="IRateLimiter"/> for one token, and short-circuits
/// with HTTP 429 + <c>Retry-After</c> when the bucket is empty.
///
/// Skipped for:
/// <list type="bullet">
///   <item>unauthenticated requests (no principal);</item>
///   <item>JWT-bearer requests (humans are never throttled by
///         this middleware — only API tokens are);</item>
///   <item>the <c>/api/account/...</c> paths (so a user can
///         always raise/lower their own limits, even when
///         throttled);</item>
///   <item>health, swagger and other non-API surfaces.</item>
/// </list>
/// </summary>
public sealed class RateLimitMiddleware(
    RequestDelegate next,
    IRateLimiter rateLimiter,
    IClock clock,
    ILogger<RateLimitMiddleware> logger)
{
    private const string ApiTokenItemKey = "ApiToken";

    /// <summary>Authentication scheme name expected on
    /// <see cref="HttpContext.User"/> for API-token requests.
    /// Must match the constant the auth handler registers as
    /// its scheme name (see
    /// <c>Cardscape.Mcp.Authentication.ApiTokenAuthenticationHandler.SchemeName</c>).</summary>
    private const string ApiTokenSchemeName = "ApiToken";

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context))
        {
            await next(context);
            return;
        }

        if (context.Items[ApiTokenItemKey] is not ApiToken token)
        {
            // The auth pipeline didn't attach the token. The
            // request is either JWT-authenticated (humans), or
            // an anonymous endpoint that slipped past the
            // skip-list. Either way, nothing to throttle.
            await next(context);
            return;
        }

        DateTimeOffset now = clock.UtcNow;

        // Reload the bucket with the latest persisted config so
        // a PATCH to the rate limit takes effect on the very
        // next request, without waiting for an instance restart.
        rateLimiter.Configure(token.Id.Value, token.RateLimitPerHour, token.BurstSize);

        RateLimitDecision decision = rateLimiter.TryAcquire(token.Id.Value, now);
        if (decision.Allowed)
        {
            await next(context);
            return;
        }

        logger.LogInformation(
            "Rate limit exceeded for API token {TokenId} on {Path}; Retry-After={RetryAfter}s",
            token.Id.Value, context.Request.Path, decision.RetryAfter);

        await WriteRateLimited(context, decision.RetryAfter);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        bool isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
        if (!isAuthenticated)
        {
            // No principal at all — anonymous endpoint (e.g.
            // /health, /api/auth/login). Let the downstream
            // pipeline handle authorization.
            return true;
        }

        // JWT-bearer request: the principal identity type comes
        // from the default JwtBearer scheme, NOT from the
        // ApiToken scheme. The MCP server uses the latter
        // exclusively; the API today only uses JWT. Either way,
        // JWT identities are humans and skip the limiter.
        if (!string.Equals(
                context.User.Identity?.AuthenticationType,
                ApiTokenSchemeName,
                StringComparison.Ordinal))
        {
            return true;
        }

        string path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/account", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static async Task WriteRateLimited(HttpContext context, int retryAfter)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Retry-After"] = retryAfter.ToString();

        string body = JsonSerializer.Serialize(new
        {
            error = "rate_limited",
            retryAfter
        });

        await context.Response.WriteAsync(body);
    }
}
