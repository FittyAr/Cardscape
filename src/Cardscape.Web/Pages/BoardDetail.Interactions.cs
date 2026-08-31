using System.Text.Json;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components.Web;

namespace Cardscape.Web.Pages;

public partial class BoardDetail
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
    private Guid? draggingCardId;

    private void OnCardDragStart(CardSummaryDto card)
    {
        draggingCardId = card.Id;
    }

    private void OnColumnDragOver(DragEventArgs args)
    {
        if (draggingCardId is not null)
        {
            args.DataTransfer!.DropEffect = "move";
        }
    }

    private async Task OnColumnDrop(Guid destinationListId)
    {
        Guid? cardId = draggingCardId;
        draggingCardId = null;
        if (cardId is null)
        {
            return;
        }

        if (await MoveCardAsync(cardId.Value, destinationListId))
        {
            await ReloadListsAndCardsAsync();
        }
    }

    // Public for the unit test: pure I/O so the
    // logic is exercised in isolation. Returns
    // true when the API accepted the move.
    public async Task<bool> MoveCardAsync(Guid cardId, Guid destinationListId)
    {
        // Drop at the end of the destination column:
        // the API's Move endpoint takes a double position;
        // we send a large sentinel so the server
        // appends rather than inserts mid-column.
        const double appendPosition = double.MaxValue;
        ApiResult<CardDto> result = await CardsApi.MoveAsync(
            cardId, destinationListId, appendPosition, CancellationToken.None);
        return result.IsSuccess;
    }

    // Pulls the board's CardAging extension and parses the
    // configured mode out of the JSON config. Failures and missing
    // extensions are both treated as Disabled (no fade) so the
    // page never breaks for an unrelated API error.
    private async Task ReloadAgingModeAsync()
    {
        now = DateTimeOffset.UtcNow;
        ApiResult<IReadOnlyList<BoardExtensionDto>> result = await ExtensionsApi.ListAsync(BoardId);
        if (!result.IsSuccess)
        {
            agingMode = CardAgingMode.Disabled;
            return;
        }

        BoardExtensionDto? match = result.Value?.FirstOrDefault(r => r.Kind == CardAgingKind);
        agingMode = match is { IsEnabled: true }
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
    // staleness window, then fade toward 0.4 (the "stale but still
    // legible" floor) over the same window.
    //  ByActivity: window = 14 days since the last update.
    private static double ComputeCardOpacity(
        CardSummaryDto card, CardAgingMode mode, DateTimeOffset now)
    {
        if (mode == CardAgingMode.Disabled)
        {
            return 1.0;
        }

        const double fadeFloor = 0.4;
        const double windowDays = 14.0;
        double daysSince = Math.Max(0, (now - card.UpdatedAt).TotalDays);
        double fade = Math.Min(1.0, daysSince / windowDays);
        return fadeFloor + (1.0 - fadeFloor) * (1.0 - fade);
    }

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
