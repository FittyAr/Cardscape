using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Queries;

public sealed record GetCardQuery(Guid CardId) : IMessage;

public static class GetCardQueryHandler
{
    public static async Task<Result<CardDto>> Handle(
        GetCardQuery query,
        ICardRepository cards,
        ICardSnoozeRepository snoozes,
        ICardMirrorRepository mirrors,
        ICommentRepository comments,
        IChecklistRepository checklists,
        IAttachmentRepository attachments,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanReadCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        // Surface the per-card snooze so the Web UI can render
        // the "Snoozed until …" badge without a second
        // round-trip. The IsSnoozed flag is derived from the
        // stored Until vs. the current time.
        CardSnooze? snooze = await snoozes.GetByCardIdAsync(card.Id, cancellationToken);

        // BETA-7-#13 — see test-results/BETA-TEST-REPORT.md.
        // A mirror card has a CardMirror row where
        // MirroredCardId == card.Id and SourceCardId is
        // the original. The Web UI surfaces the relationship
        // so the user can tell the two same-titled cards
        // apart.
        CardMirror? mirror = await mirrors.GetByMirroredCardIdAsync(card.Id, cancellationToken);

        // BUG-A5-003 — see test-results/beta/reports/A5-card-extras.md.
        // The card detail header wants the comment / attachment /
        // checklist counts so it can render the badge row next to
        // the existing member / label counts. Loading them
        // alongside the card itself saves a second round-trip;
        // the repositories expose ListForCard* / CountForCard*
        // that are already batched.
        int commentCount = (await comments.ListForCardAsync(card.Id, cancellationToken))
            .Count(c => !c.IsDeleted);
        int attachmentCount = await attachments.CountForCardAsync(card.Id.Value, cancellationToken);
        int checklistCount = (await checklists.ListForCardAsync(card.Id.Value, cancellationToken))
            .Count(c => !c.IsDeleted);

        CardDto baseDto = card.MapToDto(snooze, clock.UtcNow, mirror?.SourceCardId.Value);
        return Result.Success(baseDto with
        {
            CommentCount = commentCount,
            AttachmentCount = attachmentCount,
            ChecklistCount = checklistCount
        });
    }
}

public sealed record ListCardsForBoardQuery(
    Guid BoardId,
    bool IncludeArchived = false,
    bool IncludeSnoozed = false)
    : IMessage;

public static class ListCardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CardSummaryDto>>> Handle(
        ListCardsForBoardQuery query,
        ICardRepository cards,
        ICardSnoozeRepository snoozes,
        ICardMirrorRepository mirrors,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var guard = await MembershipGuards.EnsureCanReadBoardAsync(
            boards, currentUser.Id.Value, query.BoardId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CardSummaryDto>>(guard.Error);
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<Domain.Cards.Card> items = await cards.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId),
            query.IncludeArchived,
            cancellationToken);

        // Build a cardId → snooze lookup once for the whole
        // board so we can decorate the projections below
        // without N round-trips. Snoozes that have already
        // expired (Until <= now) are filtered out at the
        // source so the board view never sees them.
        IReadOnlyList<CardSnooze> activeSnoozes = await snoozes.ListForBoardAsync(
            query.BoardId, now, cancellationToken);
        HashSet<Guid> snoozedCardIds = new(activeSnoozes.Select(s => s.Id.Value));
        Dictionary<Guid, DateTimeOffset> snoozeUntil = activeSnoozes.ToDictionary(
            s => s.Id.Value, s => s.Until);

        // BETA-7-#13 — see test-results/BETA-TEST-REPORT.md.
        // Same bulk-build pattern as the snooze lookup above:
        // one round-trip to fetch every mirror in the board,
        // then a single dictionary lookup per card. Without
        // this, `ListCardsForBoard` left `MirrorOfCardId` at
        // its default (`null`) for every row, so the kanban
        // never rendered the mirror badge even when the
        // single-card `GetCardQuery` correctly populated it.
        IReadOnlyList<CardMirror> boardMirrors = await mirrors.ListForBoardAsync(
            query.BoardId, cancellationToken);
        Dictionary<Guid, Guid> mirrorOf = boardMirrors.ToDictionary(
            m => m.MirroredCardId.Value,
            m => m.SourceCardId.Value);

        // BETA-7-#13 — see test-results/BETA-TEST-REPORT.md.
        // Default(Guid) leaks into the DTO for non-mirror cards;
        // the Blazor template's `is not null` check then treats a
        // zero-guid as "is a mirror". Normalise to null here so
        // the UI sees a clean three-state: source (null), mirror
        // (the source id), or never (never — every card is one of
        // the first two in a fully-mirrored board).
        Guid? MirrorOf(Guid cardId) => mirrorOf.TryGetValue(cardId, out Guid src) ? src : (Guid?)null;

        // Default behaviour: snoozed cards are hidden from the
        // board view. The Web UI opt-in toggle adds
        // ?includeSnoozed=true to the request.
        IEnumerable<Domain.Cards.Card> filtered = query.IncludeSnoozed
            ? items
            : items.Where(c => !snoozedCardIds.Contains(c.Id.Value));

        var rows = filtered
            .Select(c => new CardSummaryDto(
                c.Id.Value,
                c.ListId.Value,
                c.Title.Value,
                c.Position.Value,
                c.DueDate,
                c.IsCompleted,
                // Falls back to CreatedAt so a brand-new card
                // (UpdatedAt is null until the first mutation) still
                // has a usable "last activity" timestamp for the
                // visual fade on the board.
                c.UpdatedAt ?? c.CreatedAt,
                IsSnoozed: snoozedCardIds.Contains(c.Id.Value),
                SnoozeUntil: snoozeUntil.GetValueOrDefault(c.Id.Value),
                MirrorOfCardId: MirrorOf(c.Id.Value)))
            .ToList();

        return Result.Success<IReadOnlyList<CardSummaryDto>>(rows);
    }
}
