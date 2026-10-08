using Cardscape.Api.Authentication;
using Cardscape.Api.Realtime;

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
/// the same <c>AdminOnlyAuthorizationHandler</c> as the rest of
/// the admin surface (live <c>users.IsAdmin</c> lookup unless
/// <c>CacheAdminClaim</c> is enabled). The
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
                return ApiProblemResults.ServiceUnavailable(
                    "mcp.subscriptions.unavailable",
                    "MCP subscriptions snapshot is unavailable. Check Cardscape:Mcp:BaseUrl and Internal:Secret on the API.");
            }
            return Results.Ok(snapshot);
        }).Produces<McpSubscriptionsSnapshot>();

        return app;
    }
}
