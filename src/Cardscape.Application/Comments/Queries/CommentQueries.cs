using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Comments.Queries;

public sealed record ListCommentsForCardQuery(Guid CardId) : IMessage;

public static class ListCommentsForCardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CommentDto>>> Handle(
        ListCommentsForCardQuery query,
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        ICommentRepository comments,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CommentDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // The v1.2.0 audit (pass 12) closes a read-side
        // IDOR: any authenticated user could enumerate
        // every comment on any card by guessing the
        // cardId. The fix is the same guard the
        // write-side handlers use.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, query.CardId, currentUser.Id.Value, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommentDto>>(access.Error);
        }

        var items = await comments.ListForCardAsync(new CardId(query.CardId), cancellationToken);

        // BETA-8-UI-#12 - see test-results/r8/r8-report.md.
        // Batch-load the display names for every distinct
        // author so the Web UI can render 'Alice' instead
        // of '72fc4808-bcfd-48d8-b9cf-0b38b5de6808'. The list
        // projection kept the previous rows' AuthorId raw,
        // which was correct on the wire but useless to a
        // human reading the activity feed.
        List<UserId> authorIds = items.Select(c => new UserId(c.AuthorId)).Distinct().ToList();
        Dictionary<Guid, string> displayNames = (await users.ListByIdsAsync(authorIds, cancellationToken))
            .ToDictionary(u => u.Id.Value, u => u.DisplayName.Value);

        var rows = items
            .Select(c => new CommentDto(
                c.Id.Value,
                c.CardId.Value,
                c.AuthorId,
                displayNames.GetValueOrDefault(c.AuthorId, string.Empty),
                c.Body.Value,
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(rows);
    }
}
