using Serilog;
using Serilog.Configuration;
using Serilog.Enrichers.CorrelationId;
using Serilog.Enrichers.Span;

namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Standard enrichers every host wires up: machine name, process,
/// thread, W3C trace context, the <see cref="LoggingConstants.CorrelationIdProperty"/>
/// pulled from the <see cref="Serilog.Context.LogContext"/>, and
/// the static <c>Service</c> / <c>Application</c> properties
/// identifying the emitting host.
/// </summary>
/// <remarks>
/// Kept as a dedicated extension so the same shape is reused by
/// the Api, Mcp, and (when relevant) the Web clients without
/// duplicating the enrichment chain.
/// </remarks>
public static class SerilogEnricherExtensions
{
    /// <summary>
    /// Adds the project-wide enricher chain to a
    /// <see cref="LoggerConfiguration"/>. Call this before
    /// <c>WriteTo.*</c> so every sink gets the extra properties.
    /// </summary>
    public static LoggerConfiguration UseCardscapeEnrichers(
        this LoggerConfiguration configuration,
        ServiceType service)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string serviceName = service switch
        {
            ServiceType.Api => "api",
            ServiceType.Mcp => "mcp",
            ServiceType.Web => "web",
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service type.")
        };

        return configuration
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithProcessName()
            .Enrich.WithThreadId()
            .Enrich.WithSpan()
            .Enrich.WithCorrelationId()
            .Enrich.WithProperty(LoggingConstants.ServiceProperty, serviceName)
            .Enrich.WithProperty(LoggingConstants.ApplicationProperty, $"Cardscape.{service}");
    }
}
