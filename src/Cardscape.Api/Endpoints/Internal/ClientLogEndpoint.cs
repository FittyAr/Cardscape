using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardscape.Infrastructure.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;

namespace Cardscape.Api.Endpoints.Internal;

/// <summary>
/// Companion endpoint for <c>Serilog.Sinks.BrowserHttp</c> on the
/// Blazor WASM client. Every browser-side log event the client
/// produces is POSTed here in CLEF (Compact Log Event Format)
/// JSON; this endpoint re-emits the event through the
/// server's structured logger so the file + OTel + (future) DB
/// sinks all see it. Without this relay, browser events would
/// vanish as soon as the user closes the tab.
/// </summary>
/// <remarks>
/// <para>
/// SECURITY: the previous incarnation of this endpoint was
/// anonymous and any unauthenticated POST was re-emitted as a
/// server-side log event. That was a log-injection vector — an
/// attacker could flood the log with attacker-controlled
/// <c>Fatal</c> / <c>Error</c> events, hide a real intrusion in
/// noise, or trigger paging alerts on fabricated events. The
/// endpoint now requires the same <c>X-Internal-Secret</c>
/// header the broadcast endpoint uses, AND the request body
/// is capped at 16 KB (a real CLEF event from Serilog is well
/// under that). The Serilog BrowserHttp sink cannot set custom
/// headers, so production deploys that want browser logs to
/// flow into the server logger must run the Blazor client
/// behind a reverse proxy that injects the header on the way
/// in. Local development (where the secret may be unset) gets
/// a 503 from this endpoint so a missing secret is loud, not
/// silent.
/// </para>
/// </remarks>
public static class ClientLogEndpoint
{
    private const string LogCategory = "Cardscape.ClientLogs";

    /// <summary>Hard cap on the request body. A real CLEF
    /// event from the Serilog BrowserHttp sink is under 2 KB;
    /// 16 KB gives generous headroom for verbose payloads
    /// while keeping a single attacker request well below the
    /// ASP.NET default (28.6 MB) so the endpoint cannot be
    /// abused as a DoS amplifier.</summary>
    private const int MaxBodyBytes = 16 * 1024;

    /// <summary>Header that carries the shared internal
    /// secret. The same value the broadcast endpoint uses; the
    /// Blazor host sits behind a reverse proxy that injects
    /// it before the request reaches the API.</summary>
    public const string SecretHeader = "X-Internal-Secret";

    public static IEndpointRouteBuilder MapClientLogEndpoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(LoggingConstants.ClientLogEndpoint).WithTags("Internal");

        group.MapPost("/", async (
            HttpContext http,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            // Auth: same shared secret as the broadcast
            // endpoint. Constant-time compare so a timing
            // oracle can't leak the secret byte-by-byte.
            string? expected = config["Internal:Secret"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                return Results.Problem(
                    detail: "Internal:Secret is not configured on the API; the client-log endpoint is unavailable.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            string? provided = http.Request.Headers[SecretHeader];
            if (string.IsNullOrEmpty(provided)
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(provided),
                    Encoding.UTF8.GetBytes(expected)))
            {
                return Results.Unauthorized();
            }

            // Body cap. The Content-Length header (when
            // present) lets us short-circuit before allocating
            // the read buffer; absent the cap is still
            // enforced by the read loop.
            if (http.Request.ContentLength is long advertised && advertised > MaxBodyBytes)
            {
                return Results.Problem(
                    detail: $"Client log event body exceeds the {MaxBodyBytes}-byte cap.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            byte[] buffer = new byte[MaxBodyBytes + 1];
            int read = 0;
            int chunk;
            while ((chunk = await http.Request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct)) > 0)
            {
                read += chunk;
                if (read > MaxBodyBytes)
                {
                    return Results.Problem(
                        detail: $"Client log event body exceeds the {MaxBodyBytes}-byte cap.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }
            }

            string body = Encoding.UTF8.GetString(buffer, 0, read);
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest(new { error = "Empty log event body." });
            }

            LogEvent? logEvent;
            try
            {
                using var stringReader = new StringReader(body);
                using var clefReader = new LogEventReader(stringReader);
                if (!clefReader.TryRead(out logEvent))
                {
                    return Results.BadRequest(new { error = "Empty or unparseable CLEF document." });
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                return Results.BadRequest(new { error = $"Unparseable CLEF event: {ex.Message}" });
            }

            if (logEvent is null)
            {
                return Results.BadRequest(new { error = "Empty CLEF document." });
            }

            // The event is now trustworthy (capped + secret
            // checked). Re-emit through the server's
            // structured logger. The level can still be
            // elevated by the client — that's the point of
            // relaying browser logs — but the volume is
            // bounded by the body cap and the secret.
            ILogger logger = loggerFactory.CreateLogger(LogCategory);
            logger.Log(
                MapLevel(logEvent.Level),
                logEvent.RenderMessage(),
                logEvent.Exception,
                logEvent.Properties.Select(p =>
                    new KeyValuePair<string, object?>(p.Key, CoercePropertyValue(p.Value))));

            return Results.NoContent();
        });

        return app;
    }

    private static LogLevel MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Trace,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.Information
    };

    /// <summary>
    /// CLEF property values come back as <see cref="LogEventPropertyValue"/>
    /// subclasses (<see cref="ScalarValue"/>, <see cref="SequenceValue"/>,
    /// <see cref="StructureValue"/>, <see cref="DictionaryValue"/>).
    /// The Microsoft <see cref="ILogger"/> extension accepts
    /// <c>object?</c> values; we render the structured shape
    /// back to a printable form so the server sinks get a useful
    /// representation.
    /// </summary>
    private static object? CoercePropertyValue(LogEventPropertyValue value) => value switch
    {
        ScalarValue sv => sv.Value,
        SequenceValue seq => string.Join(", ", seq.Elements.Select(CoercePropertyValue)),
        StructureValue struc => struc.Properties.Count == 0
            ? "{}"
            : "{" + string.Join(", ", struc.Properties.Select(p =>
                $"{p.Name}={CoercePropertyValue(p.Value)}")) + "}",
        DictionaryValue dict => "{" + string.Join(", ", dict.Elements.Select(kv =>
            $"[{CoercePropertyValue(kv.Key)}]={CoercePropertyValue(kv.Value)}")) + "}",
        _ => RenderFallback(value)
    };

    private static string RenderFallback(LogEventPropertyValue value)
    {
        var writer = new StringWriter();
        value.Render(writer);
        return writer.ToString();
    }
}
