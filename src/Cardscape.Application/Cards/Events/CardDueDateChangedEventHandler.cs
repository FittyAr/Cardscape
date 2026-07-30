using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Cards.Events;

/// <summary>
/// Push path for the Google Calendar sync integration.
/// </summary>
public static class CardDueDateCalendarSync
{
    public static async Task Handle(
        CardDueDateSet @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IGoogleCalendarConnectionRepository connections,
        IGoogleCalendarSyncService sync,
        ILogger logger,
        CancellationToken ct)
    {
        await PushAsync(
            @event.CardId,
            newDueDate: @event.DueDate,
            cards, lists, boards, connections, sync, logger, ct);
    }

    public static async Task Handle(
        CardDueDateCleared @event,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IGoogleCalendarConnectionRepository connections,
        IGoogleCalendarSyncService sync,
        ILogger logger,
        CancellationToken ct)
    {
        await PushAsync(
            @event.CardId,
            newDueDate: null,
            cards, lists, boards, connections, sync, logger, ct);
    }

    private static async Task PushAsync(
        CardId cardId,
        DateTimeOffset? newDueDate,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IGoogleCalendarConnectionRepository connections,
        IGoogleCalendarSyncService sync,
        ILogger logger,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(cardId, ct);
        if (card is null)
        {
            return;
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return;
        }

        Board? board = await boards.GetByIdAsync(list.BoardId, ct);
        if (board is null)
        {
            return;
        }

        IReadOnlyList<Domain.Integrations.GoogleCalendar.GoogleCalendarConnection> active =
            await connections.ListActiveForWorkspaceAsync(board.WorkspaceId, ct);

        if (active.Count == 0)
        {
            return;
        }

        string title = card.Title.Value;
        string? description = string.IsNullOrEmpty(card.Description.Value) ? null : card.Description.Value;

        foreach (Domain.Integrations.GoogleCalendar.GoogleCalendarConnection connection in active)
        {
            try
            {
                Result<string> result = await sync.PushCardDueDateAsync(
                    connection.UserId,
                    card.Id.Value,
                    title,
                    description,
                    newDueDate,
                    ct);

                if (result.IsSuccess)
                {
                    connection.RecordSyncSuccess(DateTimeOffset.UtcNow);
                    await connections.UpdateAsync(connection, ct);
                }
                else
                {
                    connection.RecordSyncError(result.Error.Message, DateTimeOffset.UtcNow);
                    await connections.UpdateAsync(connection, ct);
                    logger.LogWarning(
                        "Google Calendar push for card {CardId} user {UserId} failed: {Code} {Message}",
                        card.Id.Value, connection.UserId, result.Error.Code, result.Error.Message);
                }
            }
            catch (Exception ex)
            {
                connection.RecordSyncError(ex.GetType().Name, DateTimeOffset.UtcNow);
                await connections.UpdateAsync(connection, ct);
                logger.LogError(ex,
                    "Google Calendar push for card {CardId} user {UserId} threw.",
                    card.Id.Value, connection.UserId);
            }
        }
    }
}
