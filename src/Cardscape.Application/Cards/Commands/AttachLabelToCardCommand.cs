using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Commands;

public sealed record AttachLabelToCardCommand(Guid CardId, Guid LabelId) : IMessage;

public static class AttachLabelToCardCommandHandler
{
    public static async Task<Result<CardDto>> HandleAsync(
        AttachLabelToCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ILabelRepository labels,
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

        bool attached = card.CardLabels.All(cl => cl.LabelId.Value != label.Id.Value);
        var link = CardLabel.Create(card.Id, label.Id, clock.UtcNow);
        var result = card.AttachLabel(link, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        if (attached)
        {
            await activities.AddAsync(Activity.Record(
                guard.Value.Board.Id,
                card.Id.Value,
                currentUser.Id.Value,
                ActivityKind.LabelAdded,
                clock.UtcNow,
                new { labelId = label.Id.Value, name = label.Name.Value }), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

