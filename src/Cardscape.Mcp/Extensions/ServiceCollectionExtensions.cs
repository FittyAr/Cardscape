using Cardscape.Mcp.Authentication;
using ModelContextProtocol.Server;

namespace Cardscape.Mcp.Extensions;

public static class ServiceCollectionExtensions
{
    public const string McpServerName = "Cardscape";

    /// <summary>
    /// Registers the Model Context Protocol server, including the
    /// authentication handler, the API-token scheme, the
    /// <see cref="ICurrentUserResolver"/> implementation, and the
    /// tool / resource / prompt discovery.
    /// </summary>
    /// <remarks>
    /// The MCP C# SDK 1.4.1 (the latest stable release) only ships
    /// the <c>stdio</c> transport. The HTTP+SSE transport is in
    /// the 2.0 preview and will be wired in when Cardscape ships
    /// hosted MCP deployments in Phase 5 (or earlier if a
    /// maintainer wants it before then).
    /// </remarks>
    public static IServiceCollection AddCardscapeMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── API-token authentication ────────────────────────
        services.AddAuthentication(ApiTokenAuthenticationHandler.SchemeName)
                .AddScheme<ApiTokenAuthenticationOptions,
                           ApiTokenAuthenticationHandler>(
                    ApiTokenAuthenticationHandler.SchemeName,
                    _ => { });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserResolver, McpCurrentUserResolver>();

        // ── MCP server (stdio transport) ─────────────────────
        // The 1.4.1 SDK exposes AddMcpServer + WithStdioServerTransport
        // + WithToolsFromAssembly / WithPromptsFromAssembly /
        // WithResourcesFromAssembly. The 2.0 preview adds
        // WithHttpServerTransport + WithDistributedEventStore for
        // SSE-based live updates.
        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithResourcesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithPromptsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Maps the MCP server pipeline. The stdio transport doesn't
    /// need an HTTP endpoint, but the auth handler is still
    /// registered for future use (the HTTP transport will be
    /// added when the SDK 2.0 stable ships).
    /// </summary>
    public static WebApplication UseCardscapeMcp(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapCardscapeHealthChecks(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            service = "Cardscape.Mcp",
            timestamp = DateTime.UtcNow
        }));

        app.MapGet("/health/ready", () => Results.Ok(new
        {
            status = "ready",
            service = "Cardscape.Mcp",
            timestamp = DateTime.UtcNow
        }));

        return app;
    }
}
