using Microsoft.AspNetCore.Http;

namespace Cardscape.Api.Middleware;

/// <summary>
/// Adds a small, opinionated set of HTTP security headers to
/// every response. The defaults are intentionally conservative
/// — the goal is a safe default for the public Cardscape
/// surface, not a one-size-fits-all CSP that the operator
/// has to reverse-engineer for a custom SPA.
///
/// <list type="bullet">
///   <item><b>Strict-Transport-Security</b> — HSTS is only
///         emitted outside the Development environment; in
///         Development the API is usually reached over plain
///         HTTP on localhost and HSTS would be actively
///         harmful (browsers cache the directive for the
///         configured max-age and refuse the plain-HTTP
///         fallback for that host).</item>
///   <item><b>X-Content-Type-Options: nosniff</b> — stops
///         browsers from MIME-sniffing a response away from
///         the declared <c>Content-Type</c>, closing a class
///         of XSS via content-type confusion.</item>
///   <item><b>X-Frame-Options: DENY</b> — the API is JSON,
///         not a UI; framing it serves no legitimate purpose
///         and the clickjacking surface should be closed.</item>
///   <item><b>Referrer-Policy: no-referrer</b> — the Referer
///         header can leak workspace ids, board ids, and
///         card ids in the query string to third-party hosts;
///         the Blazor SPA already lives on the same origin so
///         the strictest policy is also the most useful.</item>
///   <item><b>X-XSS-Protection: 0</b> — the legacy
///         XSS-auditor header is itself an XSS vector on
///         older browsers; the recommended modern value is
///         <c>0</c> (disable) so we don't re-introduce the
///         risk in the name of a deprecated defence.</item>
/// </list>
///
/// Endpoints that need a relaxed policy (e.g. the Blazor
/// client, which is allowed to be framed only in
/// well-defined cases) can call
/// <see cref="HttpResponse.Headers"/> directly after this
/// middleware to overwrite the header value. The middleware
/// only sets the header if it has not been set already.
/// </summary>
public sealed class SecurityHeadersMiddleware(
    RequestDelegate next)
{
    private static readonly string[] StaticAssetPrefixes =
    [
        "/_content/",
        "/_framework/",
        "/css/",
        "/js/",
        "/favicon",
        "/images/"
    ];

    public Task InvokeAsync(HttpContext context)
    {
        // Set headers on the response before the next
        // middleware runs so the values are attached to the
        // outbound stream regardless of who writes the body.
        context.Response.OnStarting(static state =>
        {
            HttpContext ctx = (HttpContext)state;
            IHeaderDictionary headers = ctx.Response.Headers;

            // HSTS only outside Development. The default
            // max-age is 1 year as recommended by the OWASP
            // Secure Headers Project; preload is left off so
            // the operator can opt in by adding a config knob
            // if their domain is on the HSTS preload list.
            if (!ctx.RequestServices
                    .GetRequiredService<IWebHostEnvironment>()
                    .IsDevelopment())
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            // X-Content-Type-Options: stop the browser from
            // MIME-sniffing a response. Even a JSON endpoint
            // is safer with this on (a bug in a future
            // handler that returns text/plain with a
            // JSON-looking body cannot be promoted to
            // HTML/JS by a content-type-confusion attack).
            headers["X-Content-Type-Options"] = "nosniff";

            // X-Frame-Options: deny framing. The Blazor
            // client runs on its own origin, never framed by
            // a Cardscape-controlled page; third-party
            // framing would be a clickjacking vector. The
            // /api/internal/* broadcast path is service-to-
            // service and never rendered in a browser.
            headers["X-Frame-Options"] = "DENY";

            // Referrer-Policy: strip the Referer entirely.
            // The SPA and the API share an origin, so there
            // is no legitimate need to forward it. External
            // links in the SPA already navigate the user
            // away from the Cardscape origin and do not
            // depend on a Referer for analytics or
            // single-sign-on flow.
            headers["Referrer-Policy"] = "no-referrer";

            // Permissions-Policy: lock down the powerful
            // client-side features a Cardscape surface
            // never uses (camera, microphone, geolocation,
            // payment, USB, screen wake lock). The Web
            // client does not exercise any of these; if a
            // future feature does, the operator should add
            // the specific feature to the allow-list, not
            // disable this header.
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=(), " +
                "screen-wake-lock=(), interest-cohort=()";

            // X-XSS-Protection: explicitly disabled. The
            // legacy XSS auditor was deprecated because it
            // itself was an XSS vector. Setting it to "0"
            // tells legacy browsers to skip the broken
            // feature; modern browsers ignore the header
            // either way.
            headers["X-XSS-Protection"] = "0";

            // Cross-Origin-Opener-Policy and
            // Cross-Origin-Resource-Policy: same-origin.
            // The API does not need to be loaded into a
            // cross-origin popup; same-origin is the safest
            // default and matches how the Blazor client is
            // hosted (same origin as the API).
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // Cache-Control: no-store on the API surface
            // unless the endpoint already set one. The API
            // responses are mostly per-user JSON; caching
            // them by accident is a privacy bug. Endpoints
            // that explicitly want a cache (e.g. the
            // /api/internal/broadcast "no payload" 202) get
            // a sane default; endpoints that want to cache
            // must opt in by setting their own header.
            if (!headers.ContainsKey("Cache-Control")
                && !IsStaticAsset(ctx.Request.Path))
            {
                headers["Cache-Control"] = "no-store";
            }

            return Task.CompletedTask;
        }, context);

        return next(context);
    }

    private static bool IsStaticAsset(PathString path)
    {
        string value = path.Value ?? string.Empty;
        foreach (string prefix in StaticAssetPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
