using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace Cardscape.Infrastructure.Logging;

/// <summary>
/// Composition root for Serilog. Hosts (Api, Mcp) call
/// <see cref="UseCardscapeSerilog"/> as the very first thing
/// after <c>WebApplication.CreateBuilder</c> so the rest of the
/// pipeline (config providers, EF Core, hosted services, the
/// request pipeline) all see the structured logger.
/// </summary>
/// <remarks>
/// Two file streams are wired by default:
/// <list type="number">
///   <item><c>{RootPath}/{service}/{yyyyMMdd}/{service}-app.log</c> — every event.</item>
///   <item><c>{RootPath}/{service}/{yyyyMMdd}/{service}-errors.log</c> — <c>Warning</c> and above only.</item>
/// </list>
/// A console sink is always on. The OTel sink is conditional
/// on the OTel endpoint being configured (the
/// shared <c>Otel:EndpointUrl</c> key matches the MCP tracing
/// setup so logs and traces leave through the same collector).
/// </remarks>
public static class SerilogServiceCollectionExtensions
{
    private const long DefaultFileSizeLimitBytes = 100L * 1024L * 1024L;
    private const int DefaultRetainedFileCountLimit = 30;
    private const string OtlpEndpointConfigKey = "Otel:EndpointUrl";

    /// <summary>
    /// Configures the host to use Serilog with the
    /// project-wide sink / enricher / format policy. The same
    /// call works for the API and the MCP server — the
    /// <paramref name="service"/> argument only affects the log
    /// folder, the <c>Service</c> log property, and the
    /// application tag in the OTel resource.
    /// </summary>
    public static WebApplicationBuilder UseCardscapeSerilog(
        this WebApplicationBuilder builder,
        ServiceType service)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfiguration config = builder.Configuration;
        IHostEnvironment env = builder.Environment;

        string serviceName = ServiceName(service);
        RollingFileSettings fileSettings = BuildFileSettings(config, env, serviceName);
        builder.Host.UseSerilog((ctx, sp, lc) =>
        {
            lc.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(sp)
              .UseCardscapeEnrichers(service)
              .MinimumLevel.Information()
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
              .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
              .MinimumLevel.Override("System", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .WriteTo.Async(a => a.Console(
                  outputTemplate: LoggingConstants.ConsoleTemplate,
                  formatProvider: CultureInfo.InvariantCulture))
              .WriteTo.Async(a => a.File(
                  formatter: new CompactJsonFormatter(),
                  path: RollingFilePathBuilder.BuildTemplate(fileSettings, LoggingConstants.AppFileSuffix),
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: fileSettings.RetainedFileCountLimit,
                  fileSizeLimitBytes: fileSettings.FileSizeLimitBytes,
                  rollOnFileSizeLimit: true,
                  shared: true))
              .WriteTo.Logger(sub => sub
                  .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Warning)
                  .WriteTo.Async(a => a.File(
                      formatter: new CompactJsonFormatter(),
                      path: RollingFilePathBuilder.BuildTemplate(fileSettings, LoggingConstants.ErrorFileSuffix),
                      rollingInterval: RollingInterval.Day,
                      retainedFileCountLimit: fileSettings.RetainedFileCountLimit,
                      fileSizeLimitBytes: fileSettings.FileSizeLimitBytes,
                      rollOnFileSizeLimit: true,
                      shared: true)));

            string? otlpEndpoint = ctx.Configuration[OtlpEndpointConfigKey]
                ?? Environment.GetEnvironmentVariable("Otel__EndpointUrl");
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                lc.WriteTo.OpenTelemetry(opts =>
                {
                    opts.Endpoint = otlpEndpoint;
                    opts.Protocol = OtlpProtocol.Grpc;
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = $"Cardscape.{service}",
                        ["service.version"] = "1.0.0",
                        ["deployment.environment"] = env.EnvironmentName
                    };
                });
            }
        });

        return builder;
    }

    private static RollingFileSettings BuildFileSettings(
        IConfiguration config,
        IHostEnvironment env,
        string serviceName)
    {
        string? root = config["Serilog:File:RootPath"]
            ?? Environment.GetEnvironmentVariable("Serilog__File__RootPath")
            ?? LoggingConstants.DefaultLogsRoot;

        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(env.ContentRootPath, root);
        }

        int retained = config.GetValue<int?>("Serilog:File:RetainedFileCountLimit")
            ?? DefaultRetainedFileCountLimit;

        long sizeLimit = config.GetValue<long?>("Serilog:File:FileSizeLimitBytes")
            ?? DefaultFileSizeLimitBytes;

        return new RollingFileSettings
        {
            RootPath = root,
            ServiceName = serviceName,
            RetainedFileCountLimit = retained,
            FileSizeLimitBytes = sizeLimit
        };
    }

    private static string ServiceName(ServiceType service) => service switch
    {
        ServiceType.Api => "api",
        ServiceType.Mcp => "mcp",
        ServiceType.Web => "web",
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service type.")
    };
}
