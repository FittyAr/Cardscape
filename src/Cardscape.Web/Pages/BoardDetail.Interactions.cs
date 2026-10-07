using System.Text.Json;
using Cardscape.Web.Services;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Pages;

public sealed partial class BoardDetail
{
    // ── Drag-and-drop wiring ───────────────────────────────────
    // The HTML5 drag-and-drop API in Blazor's
    // Microsoft.AspNetCore.Components.Web surface does
    // not expose DataTransfer.SetData / GetData from
    // C# without a JS interop layer, and the ADR-0009
    // "Radzen only" rule prohibits custom JS. We pass
    // the source card id through component state
    // instead: dragstart records the id in a private
    // field, dragover just sets the dropEffect so the
    // browser accepts the drop, and drop reads the
    // cached id. The id is cleared at the end of a
    // successful drop so two interleaved drags do not
    // collide.
    private Guid? _draggingCardId;

    // Insertion point under the pointer. Updated on dragenter only
    // (one C# round trip per card crossed, not per mouse move) and
    // rendered as the insertion indicator.
    private CardDropTarget? _dropTarget;

    private void OnCardDragStart(CardSummaryDto card)
    {
        _draggingCardId = card.Id;
        _dropTarget = null;
    }

    private void OnCardDragEnter(CardSummaryDto hovered)
    {
        CardSummaryDto? dragged = FindCard(_draggingCardId);
        if (dragged is null)
        {
            return;
        }

        _dropTarget = hovered.Id == dragged.Id
            ? null
            : CardDropPosition.TargetFor(CardsOf(hovered.ListId), dragged, hovered);
    }

    private void OnColumnTailDragEnter(string columnId)
    {
        if (_draggingCardId is not null)
        {
            _dropTarget = new CardDropTarget(Guid.Parse(columnId), CardId: null, After: false);
        }
    }

    private void OnCardDragEnd()
    {
        _draggingCardId = null;
        _draggingListId = null;
        _dropTarget = null;
    }

    private string? DropIndicatorClass(CardSummaryDto card) =>
        _dropTarget is { CardId: { } id } target && id == card.Id && _draggingCardId is not null
            ? (target.After ? "is-drop-after" : "is-drop-before")
            : null;

    private string? DropTailColumnId =>
        _draggingCardId is not null && _dropTarget is { CardId: null } target ? target.ListId.ToString() : null;

    private async Task OnKanbanDropAsync(string columnId)
    {
        Guid listId = Guid.Parse(columnId);
        if (!await TryDropListAsync(listId))
        {
            await OnColumnDropAsync(listId);
        }
    }

    private async Task OnColumnDropAsync(Guid destinationListId)
    {
        Guid? cardId = _draggingCardId;
        CardDropTarget? target = _dropTarget;
        _draggingCardId = null;
        _dropTarget = null;
        if (cardId is null)
        {
            return;
        }

        // A stale target from another column (the pointer crossed a
        // column's padding last) degrades to "append".
        if (target is null || target.ListId != destinationListId)
        {
            target = new CardDropTarget(destinationListId, CardId: null, After: false);
        }

        double? position = CardDropPosition.Compute(CardsOf(destinationListId), cardId.Value, target);
        if (position is null)
        {
            return;
        }

        if (await MoveCardAsync(cardId.Value, destinationListId, position.Value))
        {
            await ReloadListsAndCardsAsync();
        }
    }

    private IReadOnlyList<CardSummaryDto> CardsOf(Guid listId) =>
        _cardsByList.GetValueOrDefault(listId, []);

    private CardSummaryDto? FindCard(Guid? cardId) =>
        cardId is null
            ? null
            : _cardsByList.Values.SelectMany(cards => cards).FirstOrDefault(card => card.Id == cardId);

    // Public for the unit test: pure I/O so the
    // logic is exercised in isolation. Returns
    // true when the API accepted the move.
    public async Task<bool> MoveCardAsync(Guid cardId, Guid destinationListId, double position)
    {
        ApiResult<CardDto> result = await CardsApi.MoveAsync(
            cardId, destinationListId, position, CancellationToken.None);
        return result.IsSuccess;
    }

    // Pulls the board's CardAging extension and parses the
    // configured mode out of the JSON config. Failures and missing
    // extensions are both treated as Disabled (no fade) so the
    // page never breaks for an unrelated API error.
    private async Task ReloadAgingModeAsync()
    {
        _now = DateTimeOffset.UtcNow;
        ApiResult<IReadOnlyList<BoardExtensionDto>> result = await ExtensionsApi.ListAsync(BoardId);
        if (!result.IsSuccess)
        {
            _agingMode = CardAgingMode.Disabled;
            return;
        }

        BoardExtensionDto? match = result.Value?.FirstOrDefault(r => r.Kind == CardAgingKind);
        _agingMode = match is { IsEnabled: true }
            ? ParseAgingMode(match.ConfigJson)
            : CardAgingMode.Disabled;
    }

    private static CardAgingMode ParseAgingMode(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return CardAgingMode.Disabled;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("mode", out JsonElement modeEl)
                && modeEl.ValueKind == JsonValueKind.String)
            {
                string? raw = modeEl.GetString();
                if (Enum.TryParse<CardAgingMode>(raw, ignoreCase: true, out CardAgingMode parsed)
                    && Enum.IsDefined(parsed))
                {
                    return parsed;
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to the default below.
        }

        return CardAgingMode.Disabled;
    }

    // Linear opacity: cards stay at full opacity until the mode's
    // staleness window, then fade toward 0.6 (the "stale but still
    // legible" floor) over the same window.
    //  ByActivity: window = 14 days since the last update.
    private static double ComputeCardOpacity(
        CardSummaryDto card, CardAgingMode mode, DateTimeOffset now)
    {
        if (mode == CardAgingMode.Disabled)
        {
            return 1.0;
        }

        const double fadeFloor = 0.6;
        const double windowDays = 14.0;
        double daysSince = Math.Max(0, (now - card.UpdatedAt).TotalDays);
        double fade = Math.Min(1.0, daysSince / windowDays);
        return fadeFloor + (1.0 - fadeFloor) * (1.0 - fade);
    }

    // Due-date chip colour on the kanban card.
    private enum DueState
    {
        Upcoming,
        Soon,
        Overdue,
        Done,
    }

    private static DueState GetDueState(CardSummaryDto card, DateTimeOffset now)
    {
        if (card.IsCompleted)
        {
            return DueState.Done;
        }

        if (card.DueDate is not { } due)
        {
            return DueState.Upcoming;
        }

        if (due < now)
        {
            return DueState.Overdue;
        }

        return due - now <= TimeSpan.FromHours(48) ? DueState.Soon : DueState.Upcoming;
    }

    private static string VisibilityIcon(BoardVisibility visibility) => visibility switch
    {
        BoardVisibility.Private => "lock",
        BoardVisibility.Workspace => "group",
        _ => "public",
    };

    private string VisibilityLabel(BoardVisibility visibility) => visibility switch
    {
        BoardVisibility.Private => L["BoardsVisibilityPrivate"],
        BoardVisibility.Workspace => L["BoardsVisibilityWorkspace"],
        _ => L["BoardsVisibilityPublic"],
    };

    private sealed class AddListModel
    {
        public string Name { get; set; } = string.Empty;
    }

    // Mirrors Cardscape.Domain.Cards.CardAgingMode. Kept local so
    // the Web project doesn't need a domain reference just to drive
    // the opacity math.
    private enum CardAgingMode
    {
        Disabled = 0,
        ByActivity = 1
    }
}
