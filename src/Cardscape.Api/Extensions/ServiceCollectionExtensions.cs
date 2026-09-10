using System.Text;
using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Name of the policy scheme that fronts the real JWT and API
    /// token schemes. The policy scheme picks the right inner
    /// scheme per request based on the secret's shape.
    /// </summary>
    public const string BearerPolicyScheme = "BearerPolicy";

    /// <summary>
    /// Short-lived cookie used only while an external OAuth/OIDC challenge
    /// crosses the provider boundary. It is never an API authentication scheme.
    /// </summary>
    public const string ExternalCookieScheme = "Cardscape.External";

    /// <summary>
    /// Wires both authentication schemes the REST API accepts:
    /// <list type="bullet">
    ///   <item><b>JWT bearer</b>: humans sign in via
    ///         <c>/api/auth/login</c> and present a short-lived
    ///         JWT. The signing key comes from
    ///         <c>Jwt:SigningKey</c> in configuration. In development
    ///         a stable default is used so the smoke tests have a
    ///         known secret; in production the key MUST be
    ///         overridden by configuration.</item>
    ///   <item><b>API token</b>: long-lived tokens minted via
    ///         <c>/api/security/api-tokens</c>. Used by the MCP
    ///         server and direct API consumers. These requests are
    ///         subject to the rate-limit middleware (JWT requests
    ///         bypass it).</item>
    /// </list>
    /// The default scheme is a thin policy scheme that forwards
    /// to either <c>JwtBearer</c> or <c>ApiToken</c> based on
    /// whether the bearer secret contains a dot (JWTs have
    /// three base64url segments; API tokens are a single
    /// base64url string).
    /// </summary>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddSamlMetadataClient(services);

        // The signing key is non-negotiable. A hard-coded fallback
        // (the previous behaviour) is a critical security risk: if
        // the operator forgets to set Jwt:SigningKey in production
        // the system silently uses a known secret that anyone can
        // forge. Refuse to start instead. Development still gets a
        // stable default so the smoke tests have a known secret;
        // the host environment check is what differentiates the two.
        string signingKey = ResolveJwtSigningKey(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        // CORS for the Blazor WASM client. The client
        // (http(s)://localhost:5206 / 7188 in dev) needs to be
        // allowed to call this API with credentials. In
        // production the API is expected to be served behind
        // a reverse proxy on the same origin as the SPA, so
        // the policy is intentionally permissive on localhost
        // in dev only.
        //
        // SECURITY: AllowCredentials() combined with
        // AllowAnyHeader() is a real CORS risk — the browser
        // will accept any Access-Control-Request-Headers the
        // client sends when the origin matches. Outside
        // Development, we refuse to start with the dev-only
        // localhost defaults so a missing operator override
        // is loud, not silent.
        AddApiCors(services, configuration);

        // Default scheme is a policy that picks the inner scheme
        // per request. The selector below dispatches to JWT or
        // API token based on the bearer secret's shape.
        AuthenticationBuilder authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = BearerPolicyScheme;
            options.DefaultAuthenticateScheme = BearerPolicyScheme;
            options.DefaultChallengeScheme = BearerPolicyScheme;
            options.DefaultSignInScheme = ExternalCookieScheme;
        });

        authBuilder.AddCookie(ExternalCookieScheme, options =>
        {
            options.Cookie.Name = "cardscape.external";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            options.SlidingExpiration = false;
        });

        authBuilder.AddPolicyScheme(
            BearerPolicyScheme,
            displayName: "Bearer policy (JWT or API token)",
            options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // SCIM v2 endpoints use their own bearer
                    // token scheme (the ScimToken is a
                    // random 256-bit secret, not a JWT and
                    // not an API token). Route the request
                    // to the SCIM handler when the path is
                    // under /scim/v2/; the handler itself
                    // returns NoResult on a missing header,
                    // so the per-endpoint workspace-id check
                    // is what produces the 401.
                    if (context.Request.Path.StartsWithSegments(
                        "/scim/v2", StringComparison.OrdinalIgnoreCase))
                    {
                        return ScimAuthenticationHandler.SchemeName;
                    }

                    if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    string raw = authHeader.ToString();
                    if (string.IsNullOrWhiteSpace(raw)
                        || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    string secret = raw["Bearer ".Length..].Trim();
                    if (string.IsNullOrWhiteSpace(secret))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    // JWTs have at least one dot (three segments).
                    // API tokens are base64url-encoded random bytes
                    // with no dots.
                    return secret.Contains('.')
                        ? JwtBearerDefaults.AuthenticationScheme
                        : ApiTokenAuthenticationHandler.SchemeName;
                };
            });

        authBuilder.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "Cardscape",
                ValidAudience = configuration["Jwt:Audience"] ?? "Cardscape",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            // SignalR WebSocket upgrade does not carry the
            // bearer header. Without this hook the BoardHub
            // `[Authorize]` always sees an anonymous user
            // and the JoinBoard handler throws
            // "Authentication required" on every page load,
            // degrading the real-time features to "off" —
            // see BUG #17 in test-results/BETA-TEST-REPORT.md.
            // Read the token from the `access_token` query
            // string (Microsoft.AspNetCore.SignalR
            // documented mechanism) so the connection is
            // authenticated before the hub is constructed.
            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = ctx =>
            {
                Microsoft.AspNetCore.Http.IRequestCookieCollection? cookies = ctx.Request.Cookies;
                if (cookies.TryGetValue("Cardscape.AccessToken", out string? cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    ctx.Token = cookieToken;
                    return Task.CompletedTask;
                }

                Microsoft.Extensions.Primitives.StringValues accessToken = ctx.Request.Query["access_token"];
                Microsoft.AspNetCore.Http.PathString path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }

                return Task.CompletedTask;
            };

            // Revocation gate. Wired directly in
            // AddJwtBearer so the events are attached to
            // the same JwtBearerOptions instance the
            // handler reads on the first request. The
            // handler resolves JwtRevocationValidator
            // through the per-request IServiceProvider.
            options.Events.OnTokenValidated = ctx =>
                ctx.HttpContext.RequestServices
                    .GetRequiredService<JwtRevocationValidator>()
                    .OnTokenValidatedAsync(ctx);
        });

        services.AddSingleton<JwtRevocationValidator>();

        authBuilder.AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(
            ApiTokenAuthenticationHandler.SchemeName,
            _ => { });

        AddExternalAuthenticationProviders(services, authBuilder, configuration);
        AddFederationSchemes(authBuilder);

        AddApiAuthorization(services, configuration);
        return services;
    }
}
