using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Cards;

public sealed record SetCardAgingModeCommand(Guid CardId, CardAgingMode Mode) : IMessage;

public static class SetCardAgingModeCommandHandler
{
    private const int DefaultStaleAfterDays = 30;

    public static async Task<Result> Handle(
        SetCardAgingModeCommand command,
        IRepository<Card, CardId> cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICardAgingSettingsRepository settings,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", $"Card {command.CardId} was not found."));
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (guard.IsFailure)
        {
            return Result.Failure(guard.Error);
        }

        var existing = await settings.GetByCardIdAsync(card.Id, ct);
        if (existing is null)
        {
            var createResult = CardAgingSettings.Create(
                card.Id, command.Mode, DefaultStaleAfterDays, clock.UtcNow);
            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            await settings.AddAsync(createResult.Value, ct);
        }
        else
        {
            var updateResult = existing.Update(command.Mode, existing.StaleAfterDays, clock.UtcNow);
            if (updateResult.IsFailure)
            {
                return Result.Failure(updateResult.Error);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
