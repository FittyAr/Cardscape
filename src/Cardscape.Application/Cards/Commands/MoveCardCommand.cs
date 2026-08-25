using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;
using Color = Cardscape.Domain.Common.Color;

namespace Cardscape.Application.Cards.Commands;

public sealed record MoveCardCommand(Guid CardId, Guid NewListId, double NewPosition)
    : IMessage;

public static class MoveCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        MoveCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        // The destination list must live on the same board as the
        // card. Otherwise an attacker who somehow has a target list
        // id could shuffle cards across boards they don't own.
        if (card.ListId.Value != command.NewListId)
        {
            var destinationList = await lists.GetByIdAsync(new BoardListId(command.NewListId), cancellationToken);
            if (destinationList is null || destinationList.BoardId.Value != guard.Value.Board.Id.Value)
            {
                return Result.Failure<CardDto>(DomainError.Validation(
                    "cards.invalid_move",
                    "Destination list must belong to the same board as the card."));
            }
        }

        // BETA-A4-007 — see test-results/beta/round-2/reports/A4-cards-lists.md.
        // The handler previously assigned the new position
        // directly, with no collision handling. The UI sends
        // discrete positions (1, 2, 3) for the destination
        // list; if the destination list already has a card at
        // the new position, both end up at the same slot and
        // the visual order is decided by the (position,
        // createdAt) tiebreaker — not the user. The fix is the
        // same shape as MoveListCommandHandler: list the
        // cards in the destination list, find the ones at the
        // exact collision slot, and shift them in ascending
        // (position, createdAt) order so the moved card owns
        // the slot unambiguously.
        Position newPosition = Position.From(command.NewPosition);
        IReadOnlyList<Card> destinationCards = await cards.ListForListAsync(
            new BoardListId(command.NewListId), includeArchived: false, cancellationToken);
        List<Card> colliding = destinationCards
            .Where(c => c.Id.Value != card.Id.Value
                        && !c.IsArchived
                        && Math.Abs(c.Position.Value - newPosition.Value) < double.Epsilon)
            .OrderBy(c => c.Position.Value)
            .ThenBy(c => c.CreatedAt)
            .ToList();
        double cursor = newPosition.Value;
        foreach (Card sibling in colliding)
        {
            cursor = Math.Max(cursor + 1.0d, sibling.Position.Value + 1.0d);
            sibling.Move(
                sibling.ListId,
                Position.From(cursor),
                clock.UtcNow);
        }

        var moveResult = card.Move(
            new BoardListId(command.NewListId),
            newPosition,
            clock.UtcNow);

        if (moveResult.IsFailure)
        {
            return Result.Failure<CardDto>(moveResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the move on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardMoved,
            $"{{\"listId\":\"{command.NewListId}\",\"position\":{command.NewPosition}}}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}


