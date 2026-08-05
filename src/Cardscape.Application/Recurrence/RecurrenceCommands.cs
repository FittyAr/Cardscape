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

public sealed record CardRecurrenceDto(
    Guid CardId,
    int IntervalDays,
    DateTimeOffset NextOccurrenceAt,
    bool IsActive)
{
    public static CardRecurrenceDto FromEntity(CardRecurrence r) => new(
        r.CardId.Value,
        r.IntervalDays,
        r.NextOccurrenceAt,
        r.IsActive);
}

public sealed record SetCardRecurrenceCommand(
    Guid CardId,
    int IntervalDays,
    DateTimeOffset FirstOccurrenceAt) : IMessage;

public static class SetCardRecurrenceCommandHandler
{
    public static async Task<Result<CardRecurrenceDto>> Handle(
        SetCardRecurrenceCommand command,
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
            return Result.Failure<CardRecurrenceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure<CardRecurrenceDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure<CardRecurrenceDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<CardRecurrenceDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        CardRecurrence? existing = await recurrences.GetForCardAsync(card.Id, ct);
        if (existing is not null)
        {
            var update = existing.Update(
                command.IntervalDays, command.FirstOccurrenceAt, clock.UtcNow);
            if (update.IsFailure)
            {
                return Result.Failure<CardRecurrenceDto>(update.Error);
            }
        }
        else
        {
            var create = CardRecurrence.Create(
                CardRecurrenceId.New(), card.Id,
                command.IntervalDays, command.FirstOccurrenceAt,
                currentUser.Id.Value, clock.UtcNow);
            if (create.IsFailure)
            {
                return Result.Failure<CardRecurrenceDto>(create.Error);
            }

            await recurrences.AddAsync(create.Value, ct);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success(CardRecurrenceDto.FromEntity(
            existing ?? (await recurrences.GetForCardAsync(card.Id, ct))!));
    }
}

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
