using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardscape.Api.Hubs;
using Cardscape.Application.Abstractions.Realtime;
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

    private delegate Task<bool> BroadcastHandler(
        IBoardNotifier notifier,
        Guid boardId,
        string? rawPayload,
        CancellationToken ct);

    private static readonly FrozenDictionary<string, BroadcastHandler> BroadcastHandlers =
        new Dictionary<string, BroadcastHandler>(StringComparer.Ordinal)
        {
            [nameof(IBoardClient.CardCreated)] = CreateHandler<CardEventPayload>(static (client, payload) => client.CardCreated(payload)),
            [nameof(IBoardClient.CardUpdated)] = CreateHandler<CardEventPayload>(static (client, payload) => client.CardUpdated(payload)),
            [nameof(IBoardClient.CardMoved)] = CreateHandler<CardMovedPayload>(static (client, payload) => client.CardMoved(payload)),
            [nameof(IBoardClient.CardCompleted)] = CreateHandler<CardEventPayload>(static (client, payload) => client.CardCompleted(payload)),
            [nameof(IBoardClient.CardReopened)] = CreateHandler<CardEventPayload>(static (client, payload) => client.CardReopened(payload)),
            [nameof(IBoardClient.CardArchived)] = CreateHandler<CardEventPayload>(static (client, payload) => client.CardArchived(payload)),
            [nameof(IBoardClient.CardRestored)] = CreateHandler<CardEventPayload>(static (client, payload) => client.CardRestored(payload)),
            [nameof(IBoardClient.CardAssigned)] = CreateHandler<CardAssignedPayload>(static (client, payload) => client.CardAssigned(payload)),
            [nameof(IBoardClient.CardUnassigned)] = CreateHandler<CardAssignedPayload>(static (client, payload) => client.CardUnassigned(payload)),
            [nameof(IBoardClient.CardLabelAttached)] = CreateHandler<CardLabelPayload>(static (client, payload) => client.CardLabelAttached(payload)),
            [nameof(IBoardClient.CardLabelDetached)] = CreateHandler<CardLabelPayload>(static (client, payload) => client.CardLabelDetached(payload)),
            [nameof(IBoardClient.ListCreated)] = CreateHandler<ListEventPayload>(static (client, payload) => client.ListCreated(payload)),
            [nameof(IBoardClient.ListRenamed)] = CreateHandler<ListEventPayload>(static (client, payload) => client.ListRenamed(payload)),
            [nameof(IBoardClient.ListArchived)] = CreateHandler<ListEventPayload>(static (client, payload) => client.ListArchived(payload)),
            [nameof(IBoardClient.ListRestored)] = CreateHandler<ListEventPayload>(static (client, payload) => client.ListRestored(payload)),
            [nameof(IBoardClient.CommentAdded)] = CreateHandler<CommentEventPayload>(static (client, payload) => client.CommentAdded(payload)),
            [nameof(IBoardClient.LabelCreated)] = CreateHandler<LabelEventPayload>(static (client, payload) => client.LabelCreated(payload)),
            [nameof(IBoardClient.BoardRenamed)] = CreateHandler<BoardEventPayload>(static (client, payload) => client.BoardRenamed(payload)),
            [nameof(IBoardClient.BoardStarred)] = CreateHandler<BoardEventPayload>(static (client, payload) => client.BoardStarred(payload)),
            [nameof(IBoardClient.BoardUnstarred)] = CreateHandler<BoardEventPayload>(static (client, payload) => client.BoardUnstarred(payload))
        }.ToFrozenDictionary(StringComparer.Ordinal);

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
                return ApiProblemResults.ServiceUnavailable(
                    "broadcast.unavailable",
                    "Internal:Secret is not configured on the API.");
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
                return ApiProblemResults.PayloadTooLarge(
                    "broadcast.payload_too_large",
                    $"Broadcast body exceeds the {MaxBodyBytes}-byte cap.");
            }

            byte[] buffer = new byte[MaxBodyBytes + 1];
            int read = 0;
            int chunk;
            while ((chunk = await http.Request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct)) > 0)
            {
                read += chunk;
                if (read > MaxBodyBytes)
                {
                    return ApiProblemResults.PayloadTooLarge(
                        "broadcast.payload_too_large",
                        $"Broadcast body exceeds the {MaxBodyBytes}-byte cap.");
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
                return ApiProblemResults.BadRequest(
                    "broadcast.invalid_json",
                    "Request body must be valid broadcast JSON.");
            }

            if (request is null)
            {
                return ApiProblemResults.BadRequest(
                    "broadcast.body_required",
                    "Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Method))
            {
                return ApiProblemResults.BadRequest(
                    "broadcast.method_required",
                    "method is required.");
            }

            Guid? resolvedBoardId = await ResolveBoardIdAsync(request, db, ct);
            if (resolvedBoardId is null)
            {
                return ApiProblemResults.BadRequest(
                    "broadcast.board_unresolved",
                    "Could not resolve the board for this broadcast. Pass boardId, listId, or cardId.");
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
                return ApiProblemResults.BadRequest(
                    "broadcast.payload_invalid",
                    "Payload does not match the broadcast method.");
            }
            return success
                ? Results.Accepted()
                : ApiProblemResults.BadRequest(
                    "broadcast.method_unknown",
                    $"Unknown method '{request.Method}'.");
        })
            .Accepts<BroadcastRequest>("application/json")
            .Produces(StatusCodes.Status202Accepted);

        return app;
    }

    /// <summary>
    /// Resolve the target board for a broadcast. The MCP only
    /// knows the entity it just created or mutated (a list, a
    /// card, a comment, a label), not necessarily the parent
    /// board. The API runs the same EF Core model, so it can do
    /// the lookup here with bounded, server-side EF Core queries.
    /// The strongly typed ids are mapped through value converters,
    /// so comparing their domain values translates to the underlying
    /// relational key columns without client-side table scans.
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
            Domain.Lists.BoardList? list = await db.Set<Domain.Lists.BoardList>()
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == new Domain.Lists.BoardListId(listId), ct);

            return list?.BoardId.Value;
        }

        if (request.CardId is { } cardId && cardId != Guid.Empty)
        {
            Domain.Cards.Card? card = await db.Set<Domain.Cards.Card>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == new Domain.Cards.CardId(cardId), ct);
            if (card is null)
            {
                return null;
            }

            Domain.Lists.BoardList? list = await db.Set<Domain.Lists.BoardList>()
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == card.ListId, ct);

            return list?.BoardId.Value;
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
        return BroadcastHandlers.TryGetValue(method, out BroadcastHandler? handler)
            && await handler(notifier, boardId, rawPayload, ct);
    }

    private static BroadcastHandler CreateHandler<TPayload>(
        Func<IBoardClient, TPayload, Task> dispatch)
        where TPayload : class =>
        async (notifier, boardId, rawPayload, ct) =>
        {
            TPayload? payload = string.IsNullOrWhiteSpace(rawPayload)
                ? null
                : JsonSerializer.Deserialize<TPayload>(rawPayload, PayloadOptions);
            if (payload is null)
            {
                return false;
            }

            await notifier.BroadcastAsync(boardId, client => dispatch(client, payload), ct);
            return true;
        };

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
