using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Cards;

public static class CardscapeExtensions
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
            ICardAgingSettingsRepository repo,
            IUnitOfWork uow,
            IClock clock,
            CancellationToken ct)
        {
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

    // ── Card Snooze ───────────────────────────────────────────

    public sealed record SnoozeCardCommand(Guid CardId, DateTimeOffset Until) : IMessage;

    public sealed record UnsnoozeCardCommand(Guid CardId) : IMessage;

    public static class SnoozeCardCommandHandler
    {
        public static async Task<Result<DateTimeOffset>> Handle(
            SnoozeCardCommand command,
            ICardSnoozeRepository repo,
            IUnitOfWork uow,
            ICurrentUser currentUser,
            IClock clock,
            CancellationToken ct)
        {
            if (currentUser.Id is null)
            {
                return Result.Failure<DateTimeOffset>(DomainError.Unauthenticated("auth.required", "Authentication is required."));
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
            IUnitOfWork uow,
            CancellationToken ct)
        {
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

    // ── Card Mirror ───────────────────────────────────────────

    public sealed record MirrorCardCommand(Guid SourceCardId, Guid TargetListId) : IMessage;

    public sealed record MirrorCardResult(Guid MirrorCardId);

    public static class MirrorCardCommandHandler
    {
        public static async Task<Result<MirrorCardResult>> Handle(
            MirrorCardCommand command,
            ICardRepository cards,
            IBoardListRepository lists,
            ICardMirrorRepository mirrors,
            IUnitOfWork uow,
            ICurrentUser currentUser,
            IClock clock,
            CancellationToken ct)
        {
            if (currentUser.Id is null)
            {
                return Result.Failure<MirrorCardResult>(DomainError.Unauthenticated(
                    "auth.required", "Authentication is required."));
            }

            Card? source = await cards.GetByIdAsync(new CardId(command.SourceCardId), ct);
            if (source is null)
            {
                return Result.Failure<MirrorCardResult>(DomainError.NotFound(
                    "cards.not_found", "Source card not found."));
            }
            BoardList? target = await lists.GetByIdAsync(new BoardListId(command.TargetListId), ct);
            if (target is null)
            {
                return Result.Failure<MirrorCardResult>(DomainError.NotFound(
                    "lists.not_found", "Target list not found."));
            }

            // Create the mirrored card as a real card row, then link them.
            var mirrorCard = Card.Create(
                CardId.New(),
                new BoardListId(command.TargetListId),
                source.Title,
                source.Description,
                Position.Start(),
                currentUser.Id.Value,
                clock.UtcNow);
            if (mirrorCard.IsFailure)
            {
                return Result.Failure<MirrorCardResult>(mirrorCard.Error);
            }
            await cards.AddAsync(mirrorCard.Value, ct);

            Result<CardMirror> link = CardMirror.Create(
                new CardId(command.SourceCardId),
                mirrorCard.Value.Id,
                new BoardListId(command.TargetListId),
                clock.UtcNow,
                currentUser.Id.Value);
            if (link.IsFailure)
            {
                return Result.Failure<MirrorCardResult>(link.Error);
            }
            await mirrors.AddAsync(link.Value, ct);
            await uow.SaveChangesAsync(ct);
            return Result.Success(new MirrorCardResult(mirrorCard.Value.Id.Value));
        }
    }
}
