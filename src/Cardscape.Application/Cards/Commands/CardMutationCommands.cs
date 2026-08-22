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

public sealed record CreateCardCommand(Guid ListId, string Title, string? Description)
    : IMessage;

public static class CreateCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        CreateCardCommand command,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICardRepository cards,
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

        var list = await lists.GetByIdAsync(new BoardListId(command.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<CardDto>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        var boardGuard = await MembershipGuards.EnsureCanMutateBoardAsync(
            boards, currentUser.Id.Value, list.BoardId.Value, cancellationToken);
        if (boardGuard.IsFailure)
        {
            return Result.Failure<CardDto>(boardGuard.Error);
        }

        var titleResult = CardTitle.Create(command.Title);
        if (titleResult.IsFailure)
        {
            return Result.Failure<CardDto>(titleResult.Error);
        }

        var descResult = CardDescription.Create(command.Description);
        if (descResult.IsFailure)
        {
            return Result.Failure<CardDto>(descResult.Error);
        }

        var cardResult = Card.Create(
            CardId.New(),
            new BoardListId(command.ListId),
            titleResult.Value,
            descResult.Value,
            Position.Start(),
            currentUser.Id.Value,
            clock.UtcNow);

        if (cardResult.IsFailure)
        {
            return Result.Failure<CardDto>(cardResult.Error);
        }

        await cards.AddAsync(cardResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var activity = Activity.Create(
            list.BoardId,
            cardResult.Value.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardCreated,
            $"{{\"title\":\"{cardResult.Value.Title.Value.Replace("\"", "\\\"")}\"}}",
            clock.UtcNow);
        await activities.AddAsync(activity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardDto(
            cardResult.Value.Id.Value,
            cardResult.Value.ListId.Value,
            cardResult.Value.Title.Value,
            cardResult.Value.Description.Value,
            cardResult.Value.Position.Value,
            cardResult.Value.DueDate,
            cardResult.Value.IsArchived,
            cardResult.Value.IsCompleted,
            cardResult.Value.CoverColor?.Value,
            cardResult.Value.CreatedAt,
            cardResult.Value.Members.Count,
            cardResult.Value.CardLabels.Count));
    }
}

public sealed record RenameCardCommand(Guid CardId, string NewTitle) : IMessage;

public static class RenameCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        RenameCardCommand command,
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

        var titleResult = CardTitle.Create(command.NewTitle);
        if (titleResult.IsFailure)
        {
            return Result.Failure<CardDto>(titleResult.Error);
        }

        var renameResult = card.Rename(titleResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<CardDto>(renameResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardRenamed,
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}

public sealed record ChangeCardDescriptionCommand(Guid CardId, string NewDescription)
    : IMessage;

public static class ChangeCardDescriptionCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ChangeCardDescriptionCommand command,
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

        var descResult = CardDescription.Create(command.NewDescription);
        if (descResult.IsFailure)
        {
            return Result.Failure<CardDto>(descResult.Error);
        }

        var changeResult = card.ChangeDescription(descResult.Value, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result.Failure<CardDto>(changeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardCreated, // Description change reuses the same kind until a dedicated one is added.
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}

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
