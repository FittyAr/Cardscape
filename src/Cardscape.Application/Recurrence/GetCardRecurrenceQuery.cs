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

public sealed record GetCardRecurrenceQuery(Guid CardId) : IMessage;

public static class GetCardRecurrenceQueryHandler
{
    public static async Task<Result<CardRecurrenceDto?>> Handle(
        GetCardRecurrenceQuery query,
        ICardRecurrenceRepository recurrences,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardRecurrenceDto?>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        CardRecurrence? r = await recurrences.GetForCardAsync(
            new CardId(query.CardId), ct);
        if (r is null)
        {
            return Result.Success<CardRecurrenceDto?>(null);
        }

        // The recurrence state (interval + next occurrence)
        // is operational metadata for a board. A non-member
        // reading it would learn the timing of automated
        // card creation on a board they cannot see — minor
        // information disclosure, but a clear IDOR. The
        // same rule as Set / Delete applies: any board
        // member can read it; everyone else gets 404 so we
        // don't leak the card's existence.
        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), ct);
        if (card is null)
        {
            return Result.Success<CardRecurrenceDto?>(null);
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Success<CardRecurrenceDto?>(null);
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Success<CardRecurrenceDto?>(null);
        }

        return Result.Success<CardRecurrenceDto?>(CardRecurrenceDto.FromEntity(r));
    }
}


