namespace Cardscape.Api.Middleware;

/// <summary>
/// While an account still has the temporary password an administrator set
/// (<c>User.MustChangePassword</c>), answers every API call with 403
/// <c>auth.password_change_required</c> — except signing in and out, the
/// caller's own profile reads, preferences and the change-password endpoint
/// itself. The flag is read on every request by
/// <see cref="Authentication.JwtRevocationValidator"/>, which marks the
/// principal, so an admin reset also applies to sessions already open.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    /// <summary>Claim the validator adds while the change is pending.</summary>
    public const string ClaimType = "cardscape:password_change_required";

    /// <summary>Response header the web client watches to open the change-password page.</summary>
    public const string HeaderName = "X-Cardscape-Password-Change";

    private static readonly PathString[] AlwaysOpen =
    [
        "/api/auth",
        "/api/users/me/password",
        "/api/users/me/preferences",
        "/api/internal",
        "/api/setup",
        "/health",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(ClaimType, "true")
            && context.Request.Path.StartsWithSegments("/api")
            && !AlwaysOpen.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers[HeaderName] = "required";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                title = "Password change required",
                status = StatusCodes.Status403Forbidden,
                detail = "Choose a new password to continue.",
                code = "auth.password_change_required",
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
