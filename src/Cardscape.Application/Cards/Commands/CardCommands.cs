using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
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
        ISearchIndex searchIndex,
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

        // BETA-7-#1 / #2 — see test-results/BETA-TEST-REPORT.md.
        // Populate the search index and the activity feed on
        // every write. Search is a singleton; it is
        // safe to call from the scoped handler.
        await searchIndex.IndexCardAsync(cardResult.Value, list.BoardId.Value, cancellationToken);

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
        ISearchIndex searchIndex,
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

        // BETA-7-#1 / #2 — see test-results/BETA-TEST-REPORT.md.
        await searchIndex.IndexCardAsync(card, guard.Value.Board.Id.Value, cancellationToken);
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
        ISearchIndex searchIndex,
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

        // BETA-7-#1 / #2 — re-index the search hit so a
        // snippet search picks up the new description.
        await searchIndex.IndexCardAsync(card, guard.Value.Board.Id.Value, cancellationToken);
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

        var moveResult = card.Move(
            new BoardListId(command.NewListId),
            Position.From(command.NewPosition),
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

        var result = card.Complete(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the completion on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardMoved, // CardCompleted reuses CardMoved until a dedicated kind is added.
            "{}",
            clock.UtcNow), cancellationToken);
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

        var result = card.Reopen(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the reopen on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardMoved, // Reopen reuses CardMoved until a dedicated kind is added.
            "{}",
            clock.UtcNow), cancellationToken);
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

        card.Archive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the archive on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardArchived,
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}

public sealed record DeleteCardCommand(Guid CardId) : IMessage;

public static class DeleteCardCommandHandler
{
    public static async Task<Result> Handle(
        DeleteCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        ISearchIndex searchIndex,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure(guard.Error);
        }

        Guid boardId = guard.Value.Board.Id.Value;

        // BETA-7-#2 — record the deletion before the card is
        // removed from the DB. The CardId is still valid
        // here; once the row is gone the activity feed would
        // hold a dangling foreign-key-like reference.
        await activities.AddAsync(Activity.Create(
            new Domain.Boards.BoardId(boardId),
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardArchived, // CardDeleted reuses CardArchived until a dedicated kind is added.
            $"{{\"title\":\"{card.Title.Value.Replace("\"", "\\\"")}\"}}",
            clock.UtcNow), cancellationToken);

        cards.Remove(card);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#1 — RemoveCardAsync also drops the
        // comment + checklist-item hits for the card, so the
        // search index stays consistent.
        await searchIndex.RemoveCardAsync(card.Id.Value, cancellationToken);

        return Result.Success();
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
        IActivityRepository activities,
        ISearchIndex searchIndex,
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

        // BETA-7-#1 / #2 — re-index the search hit and record
        // the restore on the activity feed.
        await searchIndex.IndexCardAsync(card, guard.Value.Board.Id.Value, cancellationToken);
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardRestored,
            "{}",
            clock.UtcNow), cancellationToken);
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
        IUserRepository users,
        INotificationRepository notifications,
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

        // BETA-2-#5 — see test-results/BETA-TEST-REPORT.md.
        //
        // The previous implementation trusted
        // `command.UserId` blindly and called
        // `card.Assign(command.UserId, ...)`. The card's
        // Assignments set ended up holding a Guid that
        // pointed to nothing — the card returned 200 and
        // the UI rendered the assignee avatar against a
        // missing user, which then made the Web UI throw
        // when it tried to resolve the display name. Verify
        // the user exists (and is active) before we record
        // the assignment. Soft-deleted / inactive users are
        // treated as "not found" so a stale link doesn't
        // resurrect in the assignee dropdown.
        var assignee = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (assignee is null || !assignee.IsActive)
        {
            return Result.Failure<CardDto>(DomainError.NotFound(
                "cards.assignee_not_found",
                "The user you tried to assign is not a known Cardscape user."));
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

        // BETA-7-#2 — record the assignment on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardAssigned,
            $"{{\"userId\":\"{command.UserId}\"}}",
            clock.UtcNow), cancellationToken);
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

        var result = card.Unassign(command.UserId, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the unassignment on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardUnassigned,
            $"{{\"userId\":\"{command.UserId}\"}}",
            clock.UtcNow), cancellationToken);
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

    /// <summary>
    /// Overload that also projects the per-card snooze state.
    /// Used by queries (GetCardQuery, ListCardsForBoardQuery) so
    /// the Web UI can render the "Snoozed" badge without a
    /// second round-trip. When <paramref name="snooze"/> is
    /// <c>null</c> the card is treated as not snoozed.
    /// </summary>
    public static CardDto MapToDto(this Card card, CardSnooze? snooze, DateTimeOffset now) =>
        snooze is null
            ? card.MapToDto()
            : card.MapToDto() with
            {
                IsSnoozed = snooze.IsActive(now),
                SnoozeUntil = snooze.Until
            };

    /// <summary>
    /// BETA-7-#13 — see test-results/BETA-TEST-REPORT.md.
    /// Overload that also projects the mirror source id
    /// (set when the card is a mirror copy of another
    /// card). The Web UI uses the flag to render a
    /// "Mirror" badge so users can tell the two cards with
    /// identical titles apart.
    /// </summary>
    public static CardDto MapToDto(this Card card, CardSnooze? snooze, DateTimeOffset now, Guid? mirrorOfCardId) =>
        (snooze is null
            ? card.MapToDto()
            : card.MapToDto() with
            {
                IsSnoozed = snooze.IsActive(now),
                SnoozeUntil = snooze.Until
            }) with { MirrorOfCardId = mirrorOfCardId };
}
