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


