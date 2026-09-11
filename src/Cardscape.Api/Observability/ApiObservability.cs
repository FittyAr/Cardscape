using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Cardscape.Api.Observability;

internal static class ApiObservability
{
    private const string DefaultServiceName = "Cardscape.Api";

    public static IServiceCollection AddApiObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? endpoint = configuration["Otel:EndpointUrl"];
        string serviceName = configuration["Otel:ServiceName"] ?? DefaultServiceName;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = static context =>
                            !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation(options => options.RecordException = true);

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
                }
            });

        return services;
    }
}
