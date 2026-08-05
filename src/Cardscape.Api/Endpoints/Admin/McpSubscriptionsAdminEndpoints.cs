using Cardscape.Api.Authentication;
using Cardscape.Api.Extensions;
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
/// Gated by the <see cref="McpSubscriptionsAdminPolicy"/>:
/// the handler reads the <c>is_admin</c> claim embedded in
/// the JWT at mint time (no per-request DB lookup). The
/// subscription event log discloses the per-URI session
/// ids of every connected AI client, which is sensitive
/// operational metadata — non-admin users (even
/// authenticated workspace Owners) get 403.
/// </summary>
public static class McpSubscriptionsAdminEndpoints
{
    public static IEndpointRouteBuilder MapMcpSubscriptionsAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/mcp-subscriptions")
            .RequireAuthorization(McpSubscriptionsAdminPolicy.Name)
            .WithTags("Admin.McpSubscriptions");

        group.MapGet("/", async Task<IResult> (McpSubscriptionsClient client, CancellationToken ct) =>
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
