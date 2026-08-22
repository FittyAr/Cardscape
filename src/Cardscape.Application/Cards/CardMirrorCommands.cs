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
}
