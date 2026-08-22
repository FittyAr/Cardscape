using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Cards;

public static partial class CardscapeExtensions
{
    // ── Card Aging ────────────────────────────────────────────

    public sealed record SetCardAgingCommand(
        Guid CardId,
        CardAgingMode Mode,
        int StaleAfterDays = 14) : IMessage;

    public sealed record SetCardAgingResult(CardAgingMode Mode, int StaleAfterDays);

    public static class SetCardAgingCommandHandler
    {
        public static async Task<Result<SetCardAgingResult>> Handle(
            SetCardAgingCommand command,
            ICardRepository cards,
            IBoardListRepository lists,
            IBoardRepository boards,
            ICardAgingSettingsRepository repo,
            IUnitOfWork uow,
            ICurrentUser currentUser,
            IClock clock,
            CancellationToken ct)
        {
            if (currentUser.Id is null)
            {
                return Result.Failure<SetCardAgingResult>(DomainError.Unauthenticated(
                    "auth.required", "Authentication is required."));
            }

            // The aging setting is per-card operational
            // metadata. The previous incarnation did not
            // check board membership — any authenticated
            // user could mutate the aging mode of any card
            // by guessing the card id. The mirror / snooze
            // fixes below use the same pattern.
            var guard = await EnsureCanMutateCardAsync(
                boards, lists, cards, command.CardId, currentUser.Id.Value, ct);
            if (guard.IsFailure)
            {
                return Result.Failure<SetCardAgingResult>(guard.Error);
            }

            CardAgingSettings? settings = await repo.GetByCardIdAsync(new CardId(command.CardId), ct);
            if (settings is null)
            {
                Result<CardAgingSettings> create = CardAgingSettings.Create(
                    new CardId(command.CardId), command.Mode, command.StaleAfterDays, clock.UtcNow);
                if (create.IsFailure)
                {
                    return Result.Failure<SetCardAgingResult>(create.Error);
                }
                await repo.AddAsync(create.Value, ct);
            }
            else
            {
                Result update = settings.Update(command.Mode, command.StaleAfterDays, clock.UtcNow);
                if (update.IsFailure)
                {
                    return Result.Failure<SetCardAgingResult>(update.Error);
                }
            }
            await uow.SaveChangesAsync(ct);
            return Result.Success(new SetCardAgingResult(command.Mode, command.StaleAfterDays));
        }
    }
}
