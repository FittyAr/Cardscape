using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Commands;
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
        ICardSnoozeRepository snoozes,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IClock clock,
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

        // Surface the per-card snooze so the Web UI can render
        // the "Snoozed until …" badge without a second
        // round-trip. The IsSnoozed flag is derived from the
        // stored Until vs. the current time.
        CardSnooze? snooze = await snoozes.GetByCardIdAsync(card.Id, cancellationToken);
        return Result.Success(card.MapToDto(snooze, clock.UtcNow));
    }
}

public sealed record ListCardsForBoardQuery(
    Guid BoardId,
    bool IncludeArchived = false,
    bool IncludeSnoozed = false)
    : IMessage;

public static class ListCardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CardSummaryDto>>> Handle(
        ListCardsForBoardQuery query,
        ICardRepository cards,
        ICardSnoozeRepository snoozes,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IClock clock,
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

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<Domain.Cards.Card> items = await cards.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId),
            query.IncludeArchived,
            cancellationToken);

        // Build a cardId → snooze lookup once for the whole
        // board so we can decorate the projections below
        // without N round-trips. Snoozes that have already
        // expired (Until <= now) are filtered out at the
        // source so the board view never sees them.
        IReadOnlyList<CardSnooze> activeSnoozes = await snoozes.ListForBoardAsync(
            query.BoardId, now, cancellationToken);
        HashSet<Guid> snoozedCardIds = new(activeSnoozes.Select(s => s.Id.Value));
        Dictionary<Guid, DateTimeOffset> snoozeUntil = activeSnoozes.ToDictionary(
            s => s.Id.Value, s => s.Until);

        // Default behaviour: snoozed cards are hidden from the
        // board view. The Web UI opt-in toggle adds
        // ?includeSnoozed=true to the request.
        IEnumerable<Domain.Cards.Card> filtered = query.IncludeSnoozed
            ? items
            : items.Where(c => !snoozedCardIds.Contains(c.Id.Value));

        var rows = filtered
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
                c.UpdatedAt ?? c.CreatedAt,
                IsSnoozed: snoozedCardIds.Contains(c.Id.Value),
                SnoozeUntil: snoozeUntil.GetValueOrDefault(c.Id.Value)))
            .ToList();

        return Result.Success<IReadOnlyList<CardSummaryDto>>(rows);
    }
}
