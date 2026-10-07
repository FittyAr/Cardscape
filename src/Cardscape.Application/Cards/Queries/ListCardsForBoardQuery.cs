using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Cards.Queries;

public sealed record ListCardsForBoardQuery(
    Guid BoardId,
    bool IncludeArchived = false,
    bool IncludeSnoozed = false)
    : IMessage;

public static class ListCardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CardSummaryDto>>> HandleAsync(
        ListCardsForBoardQuery query,
        ICardRepository cards,
        ICardSnoozeRepository snoozes,
        ICardMirrorRepository mirrors,
        IBoardRepository boards,
        ILabelRepository labels,
        IUserRepository users,
        IChecklistRepository checklists,
        ICommentRepository comments,
        IAttachmentRepository attachments,
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
        IReadOnlyList<Card> items = await cards.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId),
            query.IncludeArchived,
            cancellationToken);
        IReadOnlyList<CardSnooze> activeSnoozes = await snoozes.ListForBoardAsync(
            query.BoardId, now, cancellationToken);
        HashSet<Guid> snoozedCardIds = new(activeSnoozes.Select(snooze => snooze.Id.Value));
        Dictionary<Guid, DateTimeOffset> snoozeUntil = activeSnoozes.ToDictionary(
            snooze => snooze.Id.Value,
            snooze => snooze.Until);

        IReadOnlyList<CardMirror> boardMirrors = await mirrors.ListForBoardAsync(
            query.BoardId, cancellationToken);
        Dictionary<Guid, Guid> mirrorOf = boardMirrors.ToDictionary(
            mirror => mirror.MirroredCardId.Value,
            mirror => mirror.SourceCardId.Value);

        Guid? MirrorOf(Guid cardId) =>
            mirrorOf.TryGetValue(cardId, out Guid sourceId) ? sourceId : null;

        IEnumerable<Card> filtered = query.IncludeSnoozed
            ? items
            : items.Where(card => !snoozedCardIds.Contains(card.Id.Value));

        List<Card> visible = filtered.ToList();

        // Card-front badges: every lookup below is one batched query for
        // the whole board, so the listing stays O(1) round-trips.
        List<Guid> cardIds = visible.Select(card => card.Id.Value).ToList();
        IReadOnlyList<Label> boardLabels = await labels.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId), cancellationToken);
        Dictionary<Guid, CardSummaryLabelDto> labelsById = boardLabels.ToDictionary(
            label => label.Id.Value,
            label => new CardSummaryLabelDto(label.Id.Value, label.Name.Value, label.Color.Value));

        List<UserId> memberIds = visible
            .SelectMany(card => card.Members)
            .Select(member => member.UserId)
            .Distinct()
            .Select(id => new UserId(id))
            .ToList();
        IReadOnlyList<User> memberUsers = memberIds.Count == 0
            ? []
            : await users.ListByIdsAsync(memberIds, cancellationToken);
        Dictionary<Guid, string> displayNames = memberUsers.ToDictionary(
            user => user.Id.Value,
            user => user.DisplayName.Value);

        IReadOnlyDictionary<Guid, ChecklistProgressReadModel> progress =
            await checklists.ListProgressForCardsAsync(cardIds, cancellationToken);
        IReadOnlyDictionary<Guid, int> commentCounts =
            await comments.CountForCardsAsync(cardIds, cancellationToken);
        IReadOnlyDictionary<Guid, int> attachmentCounts =
            await attachments.CountForCardsAsync(cardIds, cancellationToken);

        List<CardSummaryDto> rows = visible
            .Select(card =>
            {
                Guid id = card.Id.Value;
                ChecklistProgressReadModel? checklist = progress.GetValueOrDefault(id);
                return new CardSummaryDto(
                    id,
                    card.ListId.Value,
                    card.Title.Value,
                    card.Position.Value,
                    card.DueDate,
                    card.IsCompleted,
                    card.UpdatedAt ?? card.CreatedAt,
                    IsSnoozed: snoozedCardIds.Contains(id),
                    SnoozeUntil: snoozeUntil.GetValueOrDefault(id),
                    MirrorOfCardId: MirrorOf(id),
                    Labels: card.CardLabels
                        .Select(cardLabel => labelsById.GetValueOrDefault(cardLabel.LabelId.Value))
                        .OfType<CardSummaryLabelDto>()
                        .ToList(),
                    Members: card.Members
                        .OrderBy(member => member.AssignedAt)
                        .Select(member => new CardSummaryMemberDto(
                            member.UserId,
                            displayNames.GetValueOrDefault(member.UserId, string.Empty)))
                        .ToList(),
                    ChecklistCompleted: checklist?.Completed ?? 0,
                    ChecklistTotal: checklist?.Total ?? 0,
                    CommentCount: commentCounts.GetValueOrDefault(id),
                    CoverColor: card.CoverColor?.Value,
                    HasDescription: !string.IsNullOrWhiteSpace(card.Description.Value),
                    AttachmentCount: attachmentCounts.GetValueOrDefault(id),
                    IsArchived: card.IsArchived);
            })
            .ToList();

        return Result.Success<IReadOnlyList<CardSummaryDto>>(rows);
    }
}
