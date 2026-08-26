using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Cards.Queries;

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
        ICardMirrorRepository mirrors,
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
        IReadOnlyList<Card> items = await cards.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId),
            query.IncludeArchived,
            cancellationToken);
        IReadOnlyList<CardSnooze> activeSnoozes = await snoozes.ListForBoardAsync(
            query.BoardId, now, cancellationToken);
        HashSet<Guid> snoozedCardIds = new(activeSnoozes.Select(snooze => snooze.Id.Value));
        Dictionary<Guid, DateTimeOffset> snoozeUntil = activeSnoozes.ToDictionary(
            snooze => snooze.Id.Value,
            snooze => snooze.Until);

        IReadOnlyList<CardMirror> boardMirrors = await mirrors.ListForBoardAsync(
            query.BoardId, cancellationToken);
        Dictionary<Guid, Guid> mirrorOf = boardMirrors.ToDictionary(
            mirror => mirror.MirroredCardId.Value,
            mirror => mirror.SourceCardId.Value);

        Guid? MirrorOf(Guid cardId) =>
            mirrorOf.TryGetValue(cardId, out Guid sourceId) ? sourceId : null;

        IEnumerable<Card> filtered = query.IncludeSnoozed
            ? items
            : items.Where(card => !snoozedCardIds.Contains(card.Id.Value));

        List<CardSummaryDto> rows = filtered
            .Select(card => new CardSummaryDto(
                card.Id.Value,
                card.ListId.Value,
                card.Title.Value,
                card.Position.Value,
                card.DueDate,
                card.IsCompleted,
                card.UpdatedAt ?? card.CreatedAt,
                IsSnoozed: snoozedCardIds.Contains(card.Id.Value),
                SnoozeUntil: snoozeUntil.GetValueOrDefault(card.Id.Value),
                MirrorOfCardId: MirrorOf(card.Id.Value)))
            .ToList();

        return Result.Success<IReadOnlyList<CardSummaryDto>>(rows);
    }
}
