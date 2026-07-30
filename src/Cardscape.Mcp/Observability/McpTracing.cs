using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Cardscape.Mcp.Observability;

/// <summary>
/// OpenTelemetry wiring for the MCP server. Every tool call
/// emits a span named <c>mcp.tool.&lt;name&gt;</c> so a
/// downstream observability backend (Tempo, Honeycomb, Jaeger)
/// can reconstruct the call graph.
///
/// Configuration:
/// <list type="bullet">
///   <item><c>Otel:EndpointUrl</c> (env: <c>Otel__EndpointUrl</c>)
///         — OTLP endpoint. When empty, the OTLP exporter is
///         not registered and spans are dropped (no-op).
///         This keeps local development free of network calls
///         and matches the behaviour of an in-process
///         <see cref="Activity"/> that no one listens to.</item>
///   <item><c>Otel:ServiceName</c> (env: <c>Otel__ServiceName</c>)
///         — defaults to <c>Cardscape.Mcp</c>.</item>
/// </list>
/// </summary>
public static class McpTracing
{
    /// <summary>ActivitySource used by every MCP tool span.</summary>
    public const string ActivitySourceName = "Cardscape.Mcp";
    public const string ServiceNameDefault = "Cardscape.Mcp";

    /// <summary>
    /// The <see cref="ActivitySource"/> all MCP tool spans are
    /// created from. Registered as a singleton on DI so the
    /// tracer provider can pick it up via
    /// <see cref="TracerProviderBuilder.AddSource(string[])"/>.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Adds the MCP server's tracing to the supplied
    /// <paramref name="services"/>. When
    /// <c>Otel:EndpointUrl</c> is empty, only the in-process
    /// <see cref="ActivitySource"/> is registered so tools can
    /// still emit spans; the OTLP exporter is not added.
    /// </summary>
    public static IServiceCollection AddMcpTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? endpoint = configuration["Otel:EndpointUrl"]
            ?? Environment.GetEnvironmentVariable("Otel__EndpointUrl");
        string serviceName = configuration["Otel:ServiceName"]
            ?? Environment.GetEnvironmentVariable("Otel__ServiceName")
            ?? ServiceNameDefault;

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(serviceName: serviceName, serviceVersion: "1.0.0")
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment",
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                            ?? "Production")
                }))
            .WithTracing(tb =>
            {
                tb.AddSource(ActivitySourceName)
                  .AddAspNetCoreInstrumentation()
                  .SetSampler(new AlwaysOnSampler());

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    tb.AddOtlpExporter(o => o.Endpoint = new Uri(endpoint));
                }
            });

        return services;
    }
}
