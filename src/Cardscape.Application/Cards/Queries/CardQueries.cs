using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Queries;

public sealed record GetCardQuery(Guid CardId) : IMessage;

public static class GetCardQueryHandler
{
    public static async Task<Result<CardDto>> Handle(
        GetCardQuery query,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanReadCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        return Result.Success(card.MapToDto());
    }
}

public sealed record ListCardsForBoardQuery(Guid BoardId, bool IncludeArchived = false)
    : IMessage;

public static class ListCardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CardSummaryDto>>> Handle(
        ListCardsForBoardQuery query,
        ICardRepository cards,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var guard = await MembershipGuards.EnsureCanReadBoardAsync(
            boards, currentUser.Id.Value, query.BoardId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CardSummaryDto>>(guard.Error);
        }

        var items = await cards.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId),
            query.IncludeArchived,
            cancellationToken);

        var rows = items
            .Select(c => new CardSummaryDto(
                c.Id.Value,
                c.ListId.Value,
                c.Title.Value,
                c.Position.Value,
                c.DueDate,
                c.IsCompleted,
                // Falls back to CreatedAt so a brand-new card
                // (UpdatedAt is null until the first mutation) still
                // has a usable "last activity" timestamp for the
                // visual fade on the board.
                c.UpdatedAt ?? c.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<CardSummaryDto>>(rows);
    }
}
