using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Cardscape.Web.Logging;

/// <summary>
/// Browser-side Serilog setup for the Blazor WASM client.
/// The Web project is its own assembly and cannot take a
/// project reference to <c>Cardscape.Infrastructure</c>, so the
/// configuration is duplicated here as a thin extension method
/// on <see cref="WebAssemblyHostBuilder"/>. Behaviour matches
/// the server-side <c>UseCardscapeSerilog</c>: structured
/// console, the same enrichers, and a relay sink that POSTs
/// events to <c>/api/internal/client-log</c> on the API.
/// </summary>
/// <remarks>
/// The <see cref="ILoggerFactory"/> that ships with
/// <c>WebAssemblyHostBuilder.CreateDefault</c> routes every
/// <c>ILogger&lt;T&gt;</c> through the Serilog pipeline once
/// <c>AddSerilog(dispose: true)</c> is registered, so the
/// existing <c>ILogger&lt;X&gt;</c> injections inside the Web
/// project pick up the new pipeline without any further wiring.
/// </remarks>
public static class SerilogBrowserLoggingExtensions
{
    private const string ServiceName = "web";
    private const string DefaultClientLogEndpoint = "api/internal/client-log";

    /// <summary>
    /// Wires Serilog as the Web client's logging backend.
    /// Called from <c>Program.cs</c> immediately after
    /// <see cref="WebAssemblyHostBuilder"/> construction so the
    /// rest of the host sees the configured logger through DI.
    /// </summary>
    public static WebAssemblyHostBuilder UseCardscapeBrowserSerilog(
        this WebAssemblyHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        WebAssemblyHostConfiguration configuration = builder.Configuration;
        string baseAddress = builder.HostEnvironment.BaseAddress;
        string endpoint = configuration["Serilog:ClientLogEndpoint"] ?? DefaultClientLogEndpoint;
        string fullEndpoint = new Uri(new Uri(baseAddress), endpoint).ToString();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Components", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Service", ServiceName)
            .Enrich.WithProperty("Application", "Cardscape.Web")
            .WriteTo.BrowserHttp(fullEndpoint)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        builder.Logging.AddSerilog(dispose: true);

        return builder;
    }
}
