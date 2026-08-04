using Cardscape.Mcp.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Mcp.Endpoints.Internal;

/// <summary>
/// Service-to-service endpoints the API calls after a
/// board-changing domain event so the MCP can fan the
/// change out to every AI client that has subscribed to
/// the matching <c>board://{id}</c> resource. The MCP runs
/// in a separate process, so its <see cref="McpResourceBroadcaster"/>
/// does not see the API's <c>DomainEventBroadcaster</c>
/// fire. The API HTTP-calls this endpoint, the MCP resolves
/// the URI from the <c>boardId</c> and calls
/// <c>McpResourceBroadcaster.BroadcastAsync</c>, which in
/// turn emits <c>notifications/resources/updated</c> to
/// every subscribed client.
///
/// Auth is the same shared secret the API uses for the
/// reverse direction (<c>Cardscape:Internal:Secret</c> /
/// <c>Internal:Secret</c>); the API forwards it in the
/// <c>X-Internal-Secret</c> header. The endpoint is
/// anonymous at the routing layer and lives under
/// <c>/api/internal/</c> so accidental exposure is visible
/// in logs and reverse proxies.
/// </summary>
public static class McpBoardEventEndpoints
{
    public const string SecretHeader = "X-Internal-Secret";

    public static IEndpointRouteBuilder MapMcpBoardEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/internal/board-event").WithTags("Internal");

        group.MapPost("/", async (
            HttpContext http,
            BoardEventRequest request,
            IConfiguration config,
            McpResourceBroadcaster broadcaster,
            CancellationToken ct) =>
        {
            if (!IsInternalAuthorised(http, config))
            {
                return Results.Unauthorized();
            }

            if (request.BoardId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "boardId is required." });
            }

            await broadcaster.BroadcastAsync(request.BoardId, ct);
            return Results.Accepted();
        });

        // Admin-only snapshot of the broadcaster state. The
        // API proxies this through its own admin endpoint
        // (/api/admin/mcp-subscriptions) so the Web UI does
        // not need to talk to the MCP process directly. The
        // endpoint is anonymous at the routing layer and
        // gated by the shared internal secret — the API
        // forwards the same X-Internal-Secret header it
        // uses for the broadcast direction.
        group.MapGet("/subscriptions", (
            HttpContext http,
            IConfiguration config,
            McpResourceBroadcaster broadcaster) =>
        {
            if (!IsInternalAuthorised(http, config))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(broadcaster.GetSnapshot());
        });

        return app;
    }

    private static bool IsInternalAuthorised(HttpContext http, IConfiguration config)
    {
        string? expected = config["Cardscape:Internal:Secret"]
            ?? config["Internal:Secret"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__INTERNAL__SECRET");
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        string? provided = http.Request.Headers[SecretHeader];
        return string.Equals(provided, expected, StringComparison.Ordinal);
    }

    /// <summary>Wire format the API sends. Only <c>BoardId</c>
    /// is required: the resource URI is derived from it
    /// (<c>board://{boardId:N}</c>) on the MCP side.</summary>
    public sealed record BoardEventRequest(Guid BoardId);
}
