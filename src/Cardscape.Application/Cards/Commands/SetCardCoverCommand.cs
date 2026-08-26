using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Commands;

public sealed record SetCardCoverCommand(Guid CardId, string? ColorName) : IMessage;

public static class SetCardCoverCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        SetCardCoverCommand command,
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

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
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

        Color? cover = null;
        if (!string.IsNullOrWhiteSpace(command.ColorName)
            && !string.Equals(command.ColorName, "none", StringComparison.OrdinalIgnoreCase))
        {
            cover = Color.Palette.ByName(command.ColorName);
            if (cover is null)
            {
                return Result.Failure<CardDto>(DomainError.Validation(
                    "cards.cover_invalid",
                    $"Cover color '{command.ColorName}' is not a known palette colour."));
            }
        }

        var result = card.SetCoverColor(cover, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}
