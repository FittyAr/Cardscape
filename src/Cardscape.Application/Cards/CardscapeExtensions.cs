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

    // ── Card Mirror ───────────────────────────────────────────

    public sealed record MirrorCardCommand(Guid SourceCardId, Guid TargetListId) : IMessage;

    public sealed record MirrorCardResult(Guid MirrorCardId);

    public static class MirrorCardCommandHandler
    {
        public static async Task<Result<MirrorCardResult>> Handle(
            MirrorCardCommand command,
            ICardRepository cards,
            IBoardListRepository lists,
            IBoardRepository boards,
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

            // The previous incarnation did not check that
            // the caller was a member of either the source
            // card's board or the target list's board. Any
            // authenticated user could mirror a card from
            // workspace-A into workspace-B, cross-leaking
            // the title + description into the target
            // workspace. Both checks are required.
            var sourceGuard = await EnsureCanReadCardAsync(
                boards, lists, source, currentUser.Id.Value, ct);
            if (sourceGuard.IsFailure)
            {
                return Result.Failure<MirrorCardResult>(sourceGuard.Error);
            }

            var targetGuard = await EnsureCanMutateListAsync(
                boards, target, currentUser.Id.Value, ct);
            if (targetGuard.IsFailure)
            {
                return Result.Failure<MirrorCardResult>(targetGuard.Error);
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

    // ── shared guard helpers ──────────────────────────────────

    private static async Task<Result> EnsureCanMutateCardAsync(
        IBoardRepository boards,
        IBoardListRepository lists,
        ICardRepository cards,
        Guid cardId,
        Guid userId,
        CancellationToken ct)
    {
        Card? card = await cards.GetByIdAsync(new CardId(cardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", $"Card {cardId} was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(userId))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        return Result.Success();
    }

    private static async Task<Result> EnsureCanReadCardAsync(
        IBoardRepository boards,
        IBoardListRepository lists,
        Card card,
        Guid userId,
        CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(userId))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of the source card's board."));
        }

        return Result.Success();
    }

    private static async Task<Result> EnsureCanMutateListAsync(
        IBoardRepository boards,
        BoardList target,
        Guid userId,
        CancellationToken ct)
    {
        Board? board = await boards.GetWithMembersAsync(target.BoardId, ct);
        if (board is null || !board.IsMember(userId))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of the target list's board."));
        }

        return Result.Success();
    }
}
