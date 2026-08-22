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
