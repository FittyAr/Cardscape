using Cardscape.Api.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Admin;

/// <summary>
/// Read-only admin endpoint that surfaces the MCP server's
/// resource-subscription state to the Web UI's
/// <c>/admin/mcp-subscriptions</c> page. The MCP and the
/// API run in separate processes; the API proxies the
/// MCP's <c>GET /api/internal/board-event/subscriptions</c>
/// endpoint over HTTP (using the same shared internal
/// secret the API uses for the reverse direction).
///
/// Today the endpoint is gated by the default
/// <c>RequireAuthorization()</c> policy (any authenticated
/// user). A future PR will introduce a dedicated
/// <c>AdminOnly</c> policy and tighten this — the
/// subscription event log discloses the per-URI session
/// ids of every connected AI client, which is sensitive
/// operational metadata. The current <c>RequireAuthorization</c>
/// gate keeps the surface behind the auth wall today;
/// the dedicated policy is the v1.3.0 follow-up.
/// </summary>
public static class McpSubscriptionsAdminEndpoints
{
    public static IEndpointRouteBuilder MapMcpSubscriptionsAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/mcp-subscriptions")
            .RequireAuthorization()
            .WithTags("Admin.McpSubscriptions");

        group.MapGet("/", async (McpSubscriptionsClient client, CancellationToken ct) =>
        {
            McpSubscriptionsSnapshot? snapshot = await client.GetSnapshotAsync(ct);
            if (snapshot is null)
            {
                return Results.Problem(
                    detail: "MCP subscriptions snapshot is unavailable. " +
                            "Check Cardscape:Mcp:BaseUrl and Internal:Secret on the API.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            return Results.Ok(snapshot);
        });

        return app;
    }
}
