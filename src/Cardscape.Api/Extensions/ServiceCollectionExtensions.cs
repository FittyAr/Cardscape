using System.Text;
using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Name of the policy scheme that fronts the real JWT and API
    /// token schemes. The policy scheme picks the right inner
    /// scheme per request based on the secret's shape.
    /// </summary>
    public const string BearerPolicyScheme = "BearerPolicy";

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
        string signingKey = configuration["Jwt:SigningKey"]
            ?? "dev-only-insecure-signing-key-please-override-in-production-32+chars";

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        // CORS for the Blazor WASM client in development. The client
        // (http(s)://localhost:5206 / 7188) needs to be allowed to
        // call this API with credentials. In production the API is
        // expected to be served behind a reverse proxy on the same
        // origin as the SPA, so the policy is intentionally permissive
        // on localhost in dev only.
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5206", "https://localhost:7188" };

        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

        // Default scheme is a policy that picks the inner scheme
        // per request. The selector below dispatches to JWT or
        // API token based on the bearer secret's shape.
        AuthenticationBuilder authBuilder = services
            .AddAuthentication(BearerPolicyScheme);

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
                    string selected = secret.Contains('.')
                        ? JwtBearerDefaults.AuthenticationScheme
                        : ApiTokenAuthenticationHandler.SchemeName;
                    Console.Error.WriteLine($"[BearerPolicyScheme] selected={selected} for path={context.Request.Path}");
                    return selected;
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

            // Revocation gate. Wired directly in
            // AddJwtBearer so the events are attached to
            // the same JwtBearerOptions instance the
            // handler reads on the first request. The
            // handler resolves JwtRevocationValidator
            // through the per-request IServiceProvider.
            options.Events ??= new JwtBearerEvents();
            options.Events.OnTokenValidated = ctx =>
                ctx.HttpContext.RequestServices
                    .GetRequiredService<JwtRevocationValidator>()
                    .OnTokenValidated(ctx);
        });

        services.AddSingleton<JwtRevocationValidator>();

        authBuilder.AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(
            ApiTokenAuthenticationHandler.SchemeName,
            _ => { });

        // ── OAuth 2.0 / OIDC external login (P4.1) ──────────
        // Google and Microsoft are wired in full. Apple
        // requires generating a JWT client_secret per Apple's
        // spec (see IAppleClientSecretGenerator + AppleClientSecretGenerator).
        string? googleClientId = configuration["Authentication:Google:ClientId"];
        string? googleClientSecret = configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(googleClientId)
            && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
        }

        string? microsoftClientId = configuration["Authentication:Microsoft:ClientId"];
        string? microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(microsoftClientId)
            && !string.IsNullOrWhiteSpace(microsoftClientSecret))
        {
            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = microsoftClientId;
                options.ClientSecret = microsoftClientSecret;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
        }

        // Apple "Sign in with Apple" uses the OIDC handler with
        // a JWT client_secret that's regenerated per token
        // request (Apple's spec, see IAppleClientSecretGenerator).
        // The handler is only registered when the full
        // Apple block is configured (TeamId + ClientId + KeyId
        // + PrivateKeyPem); otherwise the IsImplemented
        // check on ExternalProvider.Apple keeps the
        // /api/auth/external/apple/start endpoint out of
        // the menu.
        string? appleClientId = configuration["Authentication:Apple:ClientId"];
        string? appleTeamId = configuration["Authentication:Apple:TeamId"];
        string? appleKeyId = configuration["Authentication:Apple:KeyId"];
        string? applePrivateKeyPem = configuration["Authentication:Apple:PrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(appleClientId)
            && !string.IsNullOrWhiteSpace(appleTeamId)
            && !string.IsNullOrWhiteSpace(appleKeyId)
            && !string.IsNullOrWhiteSpace(applePrivateKeyPem))
        {
            services.AddSingleton<IAppleClientSecretGenerator, AppleClientSecretGenerator>();
            authBuilder.AddOpenIdConnect(ExternalProvider.Apple.WireName(), options =>
            {
                options.Authority = "https://appleid.apple.com";
                options.ClientId = appleClientId;
                // The client_secret is generated per request
                // by the AppleClientSecretGenerator, but the
                // OIDC handler insists on a static value at
                // registration time. We register a sentinel
                // here and replace it on every challenge via
                // OnRedirectToIdentityProvider.
                options.ClientSecret = "placeholder-replaced-on-redirect";
                options.CallbackPath = "/api/auth/external/apple/callback";
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("name");
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;

                options.Events.OnRedirectToIdentityProvider = ctx =>
                {
                    var generator = ctx.HttpContext.RequestServices
                        .GetRequiredService<IAppleClientSecretGenerator>();
                    ctx.ProtocolMessage.ClientSecret = generator.GenerateClientSecret(
                        TimeSpan.FromDays(180));
                    return Task.CompletedTask;
                };
            });
        }

        // ── SCIM v2 bearer auth (P4.4) ──────────────────────
        // Distinct scheme from JWT / API token / OAuth so the
        // ForwardDefaultSelector above doesn't intercept SCIM
        // requests — SCIM tokens are random 256-bit secrets
        // (no dots) that would otherwise land on the API
        // token scheme, where they wouldn't verify.
        authBuilder.AddScheme<ScimAuthenticationOptions, ScimAuthenticationHandler>(
            ScimAuthenticationHandler.SchemeName,
            _ => { });

        // ── SAML 2.0 SSO (P4.2) ───────────────────────────────
        // The custom handler intercepts the
        // /saml/{workspaceSlug}/{login,login-init,acs,metadata}
        // routes via IAuthenticationRequestHandler. The
        // Sustainsys.Saml2.AspNetCore2 package is referenced
        // for the type surface (Saml2Options + WebSso
        // SignInCommand / AcsCommand / MetadataCommand); the
        // stock Sustainsys.Saml2Handler is intentionally not
        // registered because the per-workspace IdP and ACS
        // URL make a single static scheme insufficient.
        authBuilder.AddScheme<Sustainsys.Saml2.AspNetCore2.Saml2Options, SamlAuthenticationHandler>(
            SamlAuthenticationHandler.SchemeName,
            _ => { });

        services.AddAuthorization(options =>
        {
            // AdminOnly policy: a request passes when the
            // authenticated principal carries the
            // <c>is_admin</c> claim embedded in the JWT
            // at mint time (no DB lookup). Falls back to a
            // users-table lookup for pre-v1.2.0 tokens that
            // don't carry the claim, so the migration is
            // automatic and existing sessions keep working
            // until they expire. Used by the
            // /api/admin/* endpoints (GDPR DSR, SOC 2
            // control evidence export, etc.).
            options.AddPolicy(
                AdminOnlyPolicy.Name,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new AdminOnlyRequirement()));

            // McpSubscriptionsAdmin: dedicated policy name
            // for the /api/admin/mcp-subscriptions endpoint
            // (subscription snapshot the Web UI's admin
            // page reads). Reuses the AdminOnlyRequirement
            // + cached-claim path; the distinct name is the
            // seam the operator uses to attach dedicated
            // telemetry, rate limits, or audit hooks in
            // future v1.3.0 work without affecting the
            // rest of the admin surface.
            options.AddPolicy(
                McpSubscriptionsAdminPolicy.Name,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new AdminOnlyRequirement()));
        });
        services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();
        return services;
    }
}

/// <summary>
/// Name of the AdminOnly authorisation policy. Use
/// <c>[Authorize(Policy = AdminOnlyPolicy.Name)]</c> on a
/// minimal-API group or controller to gate the surface
/// behind an <c>IsAdmin = true</c> user. The corresponding
/// handler is <see cref="AdminOnlyAuthorizationHandler"/>.
/// </summary>
public static class AdminOnlyPolicy
{
    public const string Name = "AdminOnly";
}
