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

public sealed record DetachLabelFromCardCommand(Guid CardId, Guid LabelId) : IMessage;

public static class DetachLabelFromCardCommandHandler
{
    public static async Task<Result<CardDto>> HandleAsync(
        DetachLabelFromCardCommand command,
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

        bool detached = card.CardLabels.Any(cl => cl.LabelId.Value == command.LabelId);
        var result = card.DetachLabel(new LabelId(command.LabelId), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        if (detached)
        {
            Label? label = await labels.GetByIdAsync(new LabelId(command.LabelId), cancellationToken);
            await activities.AddAsync(Activity.Record(
                guard.Value.Board.Id,
                card.Id.Value,
                currentUser.Id.Value,
                ActivityKind.LabelRemoved,
                clock.UtcNow,
                new { labelId = command.LabelId, name = label?.Name.Value }), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

