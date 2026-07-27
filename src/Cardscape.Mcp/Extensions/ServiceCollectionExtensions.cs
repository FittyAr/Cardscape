using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.DependencyInjection;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Mcp.Authentication;
using ModelContextProtocol.Server;

namespace Cardscape.Mcp.Extensions;

public static class ServiceCollectionExtensions
{
    public const string McpServerName = "Cardscape";

    /// <summary>
    /// Registers the Model Context Protocol server on top of the
    /// same Application + Infrastructure composition the REST API
    /// uses. Auth is JWT bearer (the same tokens the API issues);
    /// tools are static methods on classes decorated with
    /// <see cref="McpServerToolAttribute"/>.
    /// </summary>
    public static IServiceCollection AddCardscapeMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Composition: same as the API ─────────────────────
        services.AddCardscapeApplication();
        services.AddCardscapeInfrastructure(configuration);

        // ── Auth (JWT bearer matching the REST API) ──────────
        services.AddAuthentication(JwtBearerAuthenticationHandler.SchemeName)
                .AddScheme<JwtBearerAuthenticationOptions,
                           JwtBearerAuthenticationHandler>(
                    JwtBearerAuthenticationHandler.SchemeName,
                    options =>
                    {
                        options.Issuer = configuration["Jwt:Issuer"] ?? "Cardscape";
                        options.Audience = configuration["Jwt:Audience"] ?? "Cardscape";
                        options.SigningKey = configuration["Jwt:SigningKey"]
                            ?? "dev-only-insecure-signing-key-please-override-in-production-32+chars";
                    });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // The MCP server has its own ICurrentUser implementation so
        // Application layer handlers can read the JWT principal
        // without coupling to ASP.NET.
        services.AddScoped<ICurrentUser, McpCurrentUser>();

        // ── Real-time (MCP tools that mutate can push to the
        //    same SignalR hub the Web client listens to) ──────
        // The Mcp process does not own the hub (the API does), so
        // tools that need to broadcast go through a thin HTTP
        // client to the API's /hubs/board endpoint. For v0.2 the
        // MCP surface is read + write without broadcasting; the
        // hub side is wired in v0.3.
        services.AddHttpContextAccessor();

        // ── MCP server (stdio transport) ─────────────────────
        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithResourcesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithPromptsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Maps the MCP server pipeline. Stdio doesn't need HTTP
    /// endpoints, but we still wire auth + authorization so tools
    /// that reach into <see cref="ICurrentUser"/> get a fully
    /// resolved principal.
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
