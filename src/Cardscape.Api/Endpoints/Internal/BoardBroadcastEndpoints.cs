using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardscape.Api.Hubs;
using Cardscape.Application.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Api.Endpoints.Internal;

/// <summary>
/// Service-to-service endpoint that the MCP process calls after a
/// mutating tool to fan out the same SignalR events the API's own
/// Wolverine domain-event handlers would have produced. The MCP
/// runs in a separate process and has its own Wolverine bus, so
/// its handlers do not trigger the API's
/// <see cref="BoardNotifier"/> directly. Instead the MCP
/// HTTP-calls this endpoint with the board id (or a list id / card
/// id that the API resolves to a board), the SignalR method name,
/// and the typed payload. The API dispatches the call to
/// <see cref="IBoardClient"/> for the matching board group.
///
/// Auth is a shared secret in the <c>X-Internal-Secret</c>
/// header (the <c>Internal:Secret</c> config value). The endpoint
/// is anonymous at the routing layer and lives under
/// <c>/api/internal/</c> to make accidental exposure visible in
/// logs and reverse proxies.
/// </summary>
public static class BoardBroadcastEndpoints
{
    public const string SecretHeader = "X-Internal-Secret";

    /// <summary>Hard cap on the request body. A real
    /// broadcast payload is a small typed record; 64 KB
    /// gives generous headroom while keeping a single
    /// attacker request well below the ASP.NET default
    /// (28.6 MB).</summary>
    private const int MaxBodyBytes = 64 * 1024;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEndpointRouteBuilder MapBoardBroadcastEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/internal/broadcast").WithTags("Internal");

        group.MapPost("/", async (
            HttpContext http,
            IBoardNotifier notifier,
            IConfiguration config,
            Cardscape.Infrastructure.Persistence.CardscapeDbContext db,
            CancellationToken ct) =>
        {
            string? expected = config["Internal:Secret"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                return Results.Problem(
                    detail: "Internal:Secret is not configured on the API.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            // Constant-time compare so a timing oracle
            // can't leak the secret byte-by-byte.
            string? provided = http.Request.Headers[SecretHeader];
            if (string.IsNullOrEmpty(provided)
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(provided),
                    Encoding.UTF8.GetBytes(expected)))
            {
                return Results.Unauthorized();
            }

            // Authenticate before reading any attacker-controlled body. Then
            // enforce the cap before JSON deserialization; endpoint parameter
            // binding would consume the stream before this code can inspect it.
            if (http.Request.ContentLength is long advertised && advertised > MaxBodyBytes)
            {
                return Results.Problem(
                    detail: $"Broadcast body exceeds the {MaxBodyBytes}-byte cap.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            byte[] buffer = new byte[MaxBodyBytes + 1];
            int read = 0;
            int chunk;
            while ((chunk = await http.Request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct)) > 0)
            {
                read += chunk;
                if (read > MaxBodyBytes)
                {
                    return Results.Problem(
                        detail: $"Broadcast body exceeds the {MaxBodyBytes}-byte cap.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }
            }

            BroadcastRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<BroadcastRequest>(
                    buffer.AsSpan(0, read), PayloadOptions);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Request body must be valid broadcast JSON." });
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Method))
            {
                return Results.BadRequest(new { error = "method is required." });
            }

            Guid? resolvedBoardId = await ResolveBoardIdAsync(request, db, ct);
            if (resolvedBoardId is null)
            {
                return Results.BadRequest(new
                {
                    error = "Could not resolve the board for this broadcast. " +
                            "Pass boardId, listId, or cardId."
                });
            }

            string? raw = request.Payload.ValueKind == JsonValueKind.Undefined
                ? null
                : request.Payload.GetRawText();
            bool success;
            try
            {
                success = await DispatchAsync(notifier, resolvedBoardId.Value, request.Method, raw, ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Payload does not match the broadcast method." });
            }
            return success
                ? Results.Accepted()
                : Results.BadRequest(new { error = $"Unknown method '{request.Method}'." });
        }).Accepts<BroadcastRequest>("application/json");

        return app;
    }

    /// <summary>
    /// Resolve the target board for a broadcast. The MCP only
    /// knows the entity it just created or mutated (a list, a
    /// card, a comment, a label), not necessarily the parent
    /// board. The API runs the same EF Core model, so it can do
    /// the lookup here in one query per broadcast. EF Core 10
    /// cannot translate the strongly-typed-id access path
    /// (<c>l.Id.Value == x</c>), so the lookup streams
    /// client-side via <c>AsAsyncEnumerable</c>.
    /// </summary>
    private static async Task<Guid?> ResolveBoardIdAsync(
        BroadcastRequest request,
        Cardscape.Infrastructure.Persistence.CardscapeDbContext db,
        CancellationToken ct)
    {
        if (request.BoardId is { } b && b != Guid.Empty)
        {
            return b;
        }

        if (request.ListId is { } listId && listId != Guid.Empty)
        {
            Guid found = Guid.Empty;
            await foreach (var l in db.Set<Domain.Lists.BoardList>()
                                      .AsAsyncEnumerable()
                                      .WithCancellation(ct))
            {
                if (l.Id.Value == listId)
                {
                    found = l.BoardId.Value;
                    break;
                }
            }
            return found == Guid.Empty ? null : found;
        }

        if (request.CardId is { } cardId && cardId != Guid.Empty)
        {
            Guid cardListId = Guid.Empty;
            await foreach (var c in db.Set<Domain.Cards.Card>()
                                      .AsAsyncEnumerable()
                                      .WithCancellation(ct))
            {
                if (c.Id.Value == cardId)
                {
                    cardListId = c.ListId.Value;
                    break;
                }
            }
            if (cardListId == Guid.Empty)
            {
                return null;
            }

            Guid cardBoardId = Guid.Empty;
            await foreach (var l in db.Set<Domain.Lists.BoardList>()
                                      .AsAsyncEnumerable()
                                      .WithCancellation(ct))
            {
                if (l.Id.Value == cardListId)
                {
                    cardBoardId = l.BoardId.Value;
                    break;
                }
            }
            return cardBoardId == Guid.Empty ? null : cardBoardId;
        }

        return null;
    }

    /// <summary>
    /// Map a method name to the right <see cref="IBoardClient"/>
    /// call. Returns <c>false</c> if the method is not on the
    /// surface — the MCP is expected to stay in sync with the
    /// hub; unknown method is a deployment-skew bug, not a 500.
    /// </summary>
    private static async Task<bool> DispatchAsync(
        IBoardNotifier notifier,
        Guid boardId,
        string method,
        string? rawPayload,
        CancellationToken ct)
    {
        static T? Read<T>(string? raw) =>
            string.IsNullOrWhiteSpace(raw)
                ? default
                : JsonSerializer.Deserialize<T>(raw, PayloadOptions);

        switch (method)
        {
            case "CardCreated":
                {
                    var payload = Read<CardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardCreated(payload), ct);
                    return true;
                }
            case "CardUpdated":
                {
                    var payload = Read<CardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardUpdated(payload), ct);
                    return true;
                }
            case "CardMoved":
                {
                    var payload = Read<CardMovedPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardMoved(payload), ct);
                    return true;
                }
            case "CardCompleted":
                {
                    var payload = Read<CardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardCompleted(payload), ct);
                    return true;
                }
            case "CardReopened":
                {
                    var payload = Read<CardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardReopened(payload), ct);
                    return true;
                }
            case "CardArchived":
                {
                    var payload = Read<CardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardArchived(payload), ct);
                    return true;
                }
            case "CardRestored":
                {
                    var payload = Read<CardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardRestored(payload), ct);
                    return true;
                }
            case "CardAssigned":
                {
                    var payload = Read<CardAssignedPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardAssigned(payload), ct);
                    return true;
                }
            case "CardUnassigned":
                {
                    var payload = Read<CardAssignedPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardUnassigned(payload), ct);
                    return true;
                }
            case "CardLabelAttached":
                {
                    var payload = Read<CardLabelPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardLabelAttached(payload), ct);
                    return true;
                }
            case "CardLabelDetached":
                {
                    var payload = Read<CardLabelPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CardLabelDetached(payload), ct);
                    return true;
                }
            case "ListCreated":
                {
                    var payload = Read<ListEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.ListCreated(payload), ct);
                    return true;
                }
            case "ListRenamed":
                {
                    var payload = Read<ListEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.ListRenamed(payload), ct);
                    return true;
                }
            case "ListArchived":
                {
                    var payload = Read<ListEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.ListArchived(payload), ct);
                    return true;
                }
            case "ListRestored":
                {
                    var payload = Read<ListEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.ListRestored(payload), ct);
                    return true;
                }
            case "CommentAdded":
                {
                    var payload = Read<CommentEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.CommentAdded(payload), ct);
                    return true;
                }
            case "LabelCreated":
                {
                    var payload = Read<LabelEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.LabelCreated(payload), ct);
                    return true;
                }
            case "BoardRenamed":
                {
                    var payload = Read<BoardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.BoardRenamed(payload), ct);
                    return true;
                }
            case "BoardStarred":
                {
                    var payload = Read<BoardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.BoardStarred(payload), ct);
                    return true;
                }
            case "BoardUnstarred":
                {
                    var payload = Read<BoardEventPayload>(rawPayload);
                    if (payload is null) { return false; }
                    await notifier.BroadcastAsync(boardId, c => c.BoardUnstarred(payload), ct);
                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>Wire format the MCP sends. <c>BoardId</c> wins;
    /// if it's empty, the API looks up <c>ListId</c>; if that's
    /// empty too, the API resolves via <c>CardId</c>.</summary>
    public sealed record BroadcastRequest(
        Guid? BoardId,
        Guid? ListId,
        Guid? CardId,
        string Method,
        JsonElement Payload);
}
