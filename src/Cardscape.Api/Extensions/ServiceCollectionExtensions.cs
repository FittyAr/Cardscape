using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;

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

        AuthenticationBuilder authBuilder = AddCoreAuthentication(
            services,
            configuration,
            signingKey);

        AddExternalAuthenticationProviders(services, authBuilder, configuration);
        AddFederationSchemes(authBuilder);

        AddApiAuthorization(services, configuration);
        return services;
    }
}
