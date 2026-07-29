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
        IUnitOfWork uow,
        IClock clock,
        CancellationToken ct)
    {
        CardRecurrence? existing = await recurrences.GetForCardAsync(
            new CardId(command.CardId), ct);
        if (existing is null)
        {
            return Result.Failure(DomainError.NotFound(
                "recurrence.not_found", "Recurrence rule was not found."));
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
        CancellationToken ct)
    {
        CardRecurrence? r = await recurrences.GetForCardAsync(
            new CardId(query.CardId), ct);
        return Result.Success<CardRecurrenceDto?>(
            r is null ? null : CardRecurrenceDto.FromEntity(r));
    }
}
