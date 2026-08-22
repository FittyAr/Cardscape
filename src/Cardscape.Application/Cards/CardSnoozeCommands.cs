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
    // ── Card Snooze ───────────────────────────────────────────

    public sealed record SnoozeCardCommand(Guid CardId, DateTimeOffset Until) : IMessage;

    public sealed record UnsnoozeCardCommand(Guid CardId) : IMessage;

    public static class SnoozeCardCommandHandler
    {
        public static async Task<Result<DateTimeOffset>> Handle(
            SnoozeCardCommand command,
            ICardSnoozeRepository repo,
            ICardRepository cards,
            IBoardListRepository lists,
            IBoardRepository boards,
            IUnitOfWork uow,
            ICurrentUser currentUser,
            IClock clock,
            CancellationToken ct)
        {
            if (currentUser.Id is null)
            {
                return Result.Failure<DateTimeOffset>(DomainError.Unauthenticated("auth.required", "Authentication is required."));
            }

            // Snoozing hides a card from the default
            // board view until the snooze expires. The
            // previous incarnation accepted any CardId
            // from any authenticated user — a clear IDOR.
            // The v1.2.0 audit (pass 10) brings this in
            // line with the rest of the card write paths.
            var guard = await EnsureCanMutateCardAsync(
                boards, lists, cards, command.CardId, currentUser.Id.Value, ct);
            if (guard.IsFailure)
            {
                return Result.Failure<DateTimeOffset>(guard.Error);
            }

            Result<CardSnooze> create = CardSnooze.Create(
                new CardId(command.CardId), command.Until, currentUser.Id.Value, clock.UtcNow);
            if (create.IsFailure)
            {
                return Result.Failure<DateTimeOffset>(create.Error);
            }
            CardSnooze? existing = await repo.GetByCardIdAsync(new CardId(command.CardId), ct);
            if (existing is not null)
            {
                await repo.RemoveAsync(existing, ct);
            }
            await repo.AddAsync(create.Value, ct);
            await uow.SaveChangesAsync(ct);
            return Result.Success(command.Until);
        }
    }

    public static class UnsnoozeCardCommandHandler
    {
        public static async Task<Result> Handle(
            UnsnoozeCardCommand command,
            ICardSnoozeRepository repo,
            ICardRepository cards,
            IBoardListRepository lists,
            IBoardRepository boards,
            IUnitOfWork uow,
            ICurrentUser currentUser,
            CancellationToken ct)
        {
            if (currentUser.Id is null)
            {
                return Result.Failure(DomainError.Unauthenticated(
                    "auth.required", "Authentication is required."));
            }

            // See SnoozeCardCommandHandler. Unsnooze
            // mutates the same row so it has the same
            // membership requirement.
            var guard = await EnsureCanMutateCardAsync(
                boards, lists, cards, command.CardId, currentUser.Id.Value, ct);
            if (guard.IsFailure)
            {
                return Result.Failure(guard.Error);
            }

            CardSnooze? existing = await repo.GetByCardIdAsync(new CardId(command.CardId), ct);
            if (existing is null)
            {
                return Result.Failure(DomainError.NotFound("card_snooze.not_found", "Card is not snoozed."));
            }
            await repo.RemoveAsync(existing, ct);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
    }
}
