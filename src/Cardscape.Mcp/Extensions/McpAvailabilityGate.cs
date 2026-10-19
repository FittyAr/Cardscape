using Cardscape.Application.Abstractions.Settings;

namespace Cardscape.Mcp.Extensions;

/// <summary>
/// Answers every request under the MCP endpoint with 404 while the
/// administrator has the MCP server switched off
/// (<c>SystemSettings.Agents.McpEnabled</c>, ADR 0015). It runs before
/// authentication, so a disabled server reveals nothing, not even whether a
/// key is valid. The switch is read per request, so turning it on or off takes
/// effect immediately; sessions that were open keep failing until it is back on.
/// </summary>
public static class McpAvailabilityGate
{
    public const string DisabledErrorCode = "mcp.disabled";

    public static IApplicationBuilder UseCardscapeMcpGate(this IApplicationBuilder app, string pattern = McpEndpoint.DefaultPattern)
    {
        ArgumentNullException.ThrowIfNull(app);
        PathString path = new(pattern);
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
            {
                ISystemSettingsService settings = context.RequestServices.GetRequiredService<ISystemSettingsService>();
                if (!(await settings.GetAsync(context.RequestAborted)).Agents.McpEnabled)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(
                        new
                        {
                            type = "https://cardscape.dev/problems/mcp-disabled",
                            title = "The MCP server is turned off.",
                            status = StatusCodes.Status404NotFound,
                            code = DisabledErrorCode,
                            detail = "An administrator can turn it on in Settings > AI agents.",
                        },
                        options: null,
                        contentType: "application/problem+json",
                        cancellationToken: context.RequestAborted);
                    return;
                }
            }

            await next(context);
        });
    }
}

/// <summary>Where the MCP endpoint is mapped.</summary>
public static class McpEndpoint
{
    public const string DefaultPattern = "/mcp";
}
