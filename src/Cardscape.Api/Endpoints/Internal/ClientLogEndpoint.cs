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
/// The Blazor WASM client is served by the API on the same
/// origin, so the typical CSRF risk does not apply. The
/// <c>Serilog.Sinks.BrowserHttp</c> package only exposes the
/// endpoint URL — it cannot add custom headers — so a shared
/// secret in <c>X-Internal-Secret</c> would be dropped before
/// it reached this endpoint. Auth is therefore deliberately
/// lenient: the body is validated as a single CLEF event (an
/// attacker would have to know the schema to inject anything
/// useful), and the request is rate-limited by the
/// <see cref="Middleware.RateLimitMiddleware"/> already in the
/// pipeline.
/// </para>
/// <para>
/// A future iteration could move the ingestion behind an
/// authenticated user identity (e.g. the same JWT the Web
/// client uses) once the BrowserHttp sink supports a headers
/// option. Until then, an unauthenticated POST is treated as a
/// trusted client event.
/// </para>
/// </remarks>
public static class ClientLogEndpoint
{
    private const string LogCategory = "Cardscape.Web.Client";

    public static IEndpointRouteBuilder MapClientLogEndpoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(LoggingConstants.ClientLogEndpoint).WithTags("Internal");

        group.MapPost("/", async (
            HttpContext http,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            string body;
            using (var reader = new StreamReader(http.Request.Body))
            {
                body = await reader.ReadToEndAsync(ct);
            }

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
