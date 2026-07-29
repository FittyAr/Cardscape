using System.Text.Json;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Queries;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Cards.Queries;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Lists.Queries;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Tools;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Resources;

/// <summary>
/// MCP resources exposed to AI clients. Resources are
/// addressable by URI; the AI can subscribe to receive
/// updates when the underlying state changes.
///
/// The URI scheme is:
///   <c>workspace://{workspaceId}</c>
///   <c>board://{boardId}</c>
///   <c>card://{cardId}</c>
///   <c>cards://board/{boardId}</c> — list of cards on a board
///   <c>lists://board/{boardId}</c> — list of lists on a board
///
/// Every resource returns JSON. Errors are surfaced as a
/// JSON envelope with <c>error</c> and <c>message</c> fields.
/// </summary>
[McpServerResourceType]
public sealed class McpResources
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerResource(Name = "workspace", UriTemplate = "workspace://{workspaceId}")]
    public async Task<string> GetWorkspace(Uri uri, CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Guid workspaceId = ExtractGuid(uri, "workspaceId");
        Result<WorkspaceDto> result = await bus.InvokeAsync<Result<WorkspaceDto>>(
            new GetWorkspaceQuery(workspaceId), ct);
        return ToJson(uri, result);
    }

    [McpServerResource(Name = "board", UriTemplate = "board://{boardId}")]
    public async Task<string> GetBoard(Uri uri, CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Guid boardId = ExtractGuid(uri, "boardId");
        Result<BoardDto> result = await bus.InvokeAsync<Result<BoardDto>>(
            new GetBoardQuery(boardId), ct);
        return ToJson(uri, result);
    }

    [McpServerResource(Name = "card", UriTemplate = "card://{cardId}")]
    public async Task<string> GetCard(Uri uri, CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Guid cardId = ExtractGuid(uri, "cardId");
        Result<CardDto> result = await bus.InvokeAsync<Result<CardDto>>(
            new GetCardQuery(cardId), ct);
        return ToJson(uri, result);
    }

    [McpServerResource(Name = "cards-on-board", UriTemplate = "cards://board/{boardId}")]
    public async Task<string> ListCardsOnBoard(Uri uri, CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Guid boardId = ExtractGuid(uri, "boardId");
        Result<IReadOnlyList<CardSummaryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
            new ListCardsForBoardQuery(boardId, IncludeArchived: false), ct);
        return ToJson(uri, result);
    }

    [McpServerResource(Name = "lists-on-board", UriTemplate = "lists://board/{boardId}")]
    public async Task<string> ListListsOnBoard(Uri uri, CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        Guid boardId = ExtractGuid(uri, "boardId");
        Result<IReadOnlyList<BoardListDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<BoardListDto>>>(
            new ListListsForBoardQuery(boardId, IncludeArchived: false), ct);
        return ToJson(uri, result);
    }

    private static Guid ExtractGuid(Uri uri, string paramName)
    {
        string last = uri.Segments[^1].TrimEnd('/');
        if (!Guid.TryParse(last, out Guid id))
        {
            throw new ArgumentException(
                $"URI '{uri}' does not contain a valid {paramName} (expected a GUID).");
        }
        return id;
    }

    private static string ToJson<T>(Uri uri, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return JsonSerializer.Serialize(result.Value, JsonOptions);
        }
        return JsonSerializer.Serialize(new
        {
            uri = uri.ToString(),
            error = result.Error.Code,
            message = result.Error.Message
        }, JsonOptions);
    }
}
