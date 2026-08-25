using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Recurrence;
using Wolverine;

namespace Cardscape.Application.Recurrence;

public sealed record DeleteCardRecurrenceCommand(Guid CardId) : IMessage;

public static class DeleteCardRecurrenceCommandHandler
{
    public static async Task<Result> Handle(
        DeleteCardRecurrenceCommand command,
        ICardRecurrenceRepository recurrences,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IUnitOfWork uow,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        CardRecurrence? existing = await recurrences.GetForCardAsync(
            new CardId(command.CardId), ct);
        if (existing is null)
        {
            return Result.Failure(DomainError.NotFound(
                "recurrence.not_found", "Recurrence rule was not found."));
        }

        // The recurrence belongs to a card; the card lives
        // in a list; the list belongs to a board. The
        // previous incarnation of this handler skipped the
        // board-membership check entirely, so any
        // authenticated user who could guess (or scrape) a
        // card id could turn off another board's recurring
        // cards. The same rule as SetCardRecurrence applies
        // here: any board member can manage recurrences.
        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        existing.Deactivate(clock.UtcNow);
        recurrences.Remove(existing);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}


