using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Notifications;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

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

        var moveResult = card.Move(
            new BoardListId(command.NewListId),
            Position.From(command.NewPosition),
            clock.UtcNow);

        if (moveResult.IsFailure)
        {
            return Result.Failure<CardDto>(moveResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record SetCardDueDateCommand(Guid CardId, DateTimeOffset DueDate)
    : IMessage;

public static class SetCardDueDateCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        SetCardDueDateCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.SetDueDate(command.DueDate, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ClearCardDueDateCommand(Guid CardId) : IMessage;

public static class ClearCardDueDateCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ClearCardDueDateCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.ClearDueDate(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record CompleteCardCommand(Guid CardId) : IMessage;

public static class CompleteCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        CompleteCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.Complete(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ReopenCardCommand(Guid CardId) : IMessage;

public static class ReopenCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ReopenCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.Reopen(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ArchiveCardCommand(Guid CardId) : IMessage;

public static class ArchiveCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ArchiveCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        card.Archive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record RestoreCardCommand(Guid CardId) : IMessage;

public static class RestoreCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        RestoreCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        card.Restore(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record AssignCardCommand(Guid CardId, Guid UserId) : IMessage;

public static class AssignCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        AssignCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.Assign(command.UserId, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        // Notify the assignee (skip self-assign to avoid noise).
        if (command.UserId != currentUser.Id.Value)
        {
            string payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                cardId = card.Id.Value.ToString(),
                cardTitle = card.Title.Value,
                assignedBy = currentUser.Id.Value.ToString(),
                boardId = guard.Value.Board.Id.Value.ToString()
            });
            await notifications.AddAsync(
                Notification.Create(command.UserId, NotificationKind.AssignedToCard, payload, clock.UtcNow),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record UnassignCardCommand(Guid CardId, Guid UserId) : IMessage;

public static class UnassignCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        UnassignCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.Unassign(command.UserId, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record AttachLabelToCardCommand(Guid CardId, Guid LabelId) : IMessage;

public static class AttachLabelToCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        AttachLabelToCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ILabelRepository labels,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var label = await labels.GetByIdAsync(new LabelId(command.LabelId), cancellationToken);
        if (label is null)
        {
            return Result.Failure<CardDto>(DomainError.NotFound(
                "labels.not_found", "Label was not found."));
        }

        // The label must live on the same board as the card;
        // otherwise a member of board A could attach arbitrary
        // labels from board B to a card in board A.
        if (label.BoardId.Value != guard.Value.Board.Id.Value)
        {
            return Result.Failure<CardDto>(DomainError.Validation(
                "labels.wrong_board",
                "Label must belong to the same board as the card."));
        }

        var link = CardLabel.Create(card.Id, label.Id, clock.UtcNow);
        var result = card.AttachLabel(link, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record DetachLabelFromCardCommand(Guid CardId, Guid LabelId) : IMessage;

public static class DetachLabelFromCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        DetachLabelFromCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
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

        var result = card.DetachLabel(new LabelId(command.LabelId), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public static class CardMappingExtensions
{
    public static CardDto MapToDto(this Card card) => new(
        card.Id.Value,
        card.ListId.Value,
        card.Title.Value,
        card.Description.Value,
        card.Position.Value,
        card.DueDate,
        card.IsArchived,
        card.IsCompleted,
        card.CoverColor?.Value,
        card.CreatedAt,
        card.Members.Count,
        card.CardLabels.Count);
}
