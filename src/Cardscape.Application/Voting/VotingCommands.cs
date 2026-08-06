using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Voting;
using Wolverine;

namespace Cardscape.Application.Voting;

/// <summary>DTO for a single vote row. The aggregate state is
/// derived elsewhere (<see cref="CardVoteStateDto"/>).</summary>
public sealed record CardVoteDto(Guid UserId, DateTimeOffset VotedAt);

/// <summary>Shape of the vote surface for one card. Returned by
/// both the toggle and the read endpoint so the Web UI can
/// render the heart + count without a second round trip.</summary>
public sealed record CardVoteStateDto(
    Guid CardId,
    int VoteCount,
    bool CurrentUserHasVoted);

public sealed record ToggleCardVoteCommand(Guid CardId) : IMessage;

public static class ToggleCardVoteCommandHandler
{
    public static async Task<Result<CardVoteStateDto>> Handle(
        ToggleCardVoteCommand command,
        ICardVoteRepository votes,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        // Membership check via the card's board. The voting slice
        // is enabled per board via BoardExtensions, but the simpler
        // and safer default is "any board member can vote on any
        // card in the board" — the same rule as comments.
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure<CardVoteStateDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<CardVoteStateDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        // BETA-3-#2 — see test-results/BETA-TEST-REPORT.md.
        //
        // Atomic DELETE-or-INSERT on the (CardId, UserId) pair.
        // The previous "read HasVotedAsync, branch, save"
        // pattern was vulnerable to a TOCTOU race when the
        // same user toggled from two browser tabs at once:
        // both calls would observe HasVoted=false, both
        // INSERT, the second INSERT would violate the unique
        // index and surface as 500. ToggleAsync wraps the
        // pair in a SQLite transaction and re-reads the
        // state after the commit, so the returned DTO is
        // always consistent with the actual DB state.
        VoteToggleResult toggled = await votes.ToggleAsync(
            card.Id, currentUser.Id.Value, clock.UtcNow, cancellationToken);

        return Result.Success(new CardVoteStateDto(
            CardId: card.Id.Value,
            VoteCount: toggled.VoteCount,
            CurrentUserHasVoted: toggled.NowVoted));
    }
}

public sealed record ListCardVotesQuery(Guid CardId) : IMessage;

public static class ListCardVotesQueryHandler
{
    public static async Task<Result<CardVoteStateDto>> Handle(
        ListCardVotesQuery query,
        ICardVoteRepository votes,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardVoteStateDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure<CardVoteStateDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<CardVoteStateDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        int count = await votes.CountForCardAsync(card.Id, cancellationToken);
        bool hasVoted = await votes.HasVotedAsync(
            card.Id, currentUser.Id.Value, cancellationToken);

        return Result.Success(new CardVoteStateDto(
            CardId: card.Id.Value,
            VoteCount: count,
            CurrentUserHasVoted: hasVoted));
    }
}
