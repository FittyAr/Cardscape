using Cardscape.Mcp.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Mcp.Endpoints.Internal;

/// <summary>
/// Service-to-service endpoint the API calls after a
/// board-changing domain event so the MCP can fan the change
/// out to every AI client that has subscribed to the matching
/// <c>board://{id}</c> resource. The MCP runs in a separate
/// process, so its <see cref="McpResourceBroadcaster"/> does
/// not see the API's <c>DomainEventBroadcaster</c> fire. The
/// API HTTP-calls this endpoint, the MCP resolves the URI
/// from the <c>boardId</c> and calls
/// <c>McpResourceBroadcaster.BroadcastAsync</c>, which in
/// turn emits <c>notifications/resources/updated</c> to every
/// subscribed client.
///
/// Auth is the same shared secret the API uses for the
/// reverse direction (<c>Cardscape:Internal:Secret</c> /
/// <c>Internal:Secret</c>); the API forwards it in the
/// <c>X-Internal-Secret</c> header. The endpoint is anonymous
/// at the routing layer and lives under <c>/api/internal/</c>
/// so accidental exposure is visible in logs and reverse
/// proxies.
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
            string? expected = config["Cardscape:Internal:Secret"]
                ?? config["Internal:Secret"]
                ?? Environment.GetEnvironmentVariable("CARDS_CAPE__INTERNAL__SECRET");
            if (string.IsNullOrWhiteSpace(expected))
            {
                return Results.Problem(
                    detail: "Cardscape:Internal:Secret is not configured on the MCP.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            string? provided = http.Request.Headers[SecretHeader];
            if (!string.Equals(provided, expected, StringComparison.Ordinal))
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

        return app;
    }

    /// <summary>Wire format the API sends. Only <c>BoardId</c>
    /// is required: the resource URI is derived from it
    /// (<c>board://{boardId:N}</c>) on the MCP side.</summary>
    public sealed record BoardEventRequest(Guid BoardId);
}
