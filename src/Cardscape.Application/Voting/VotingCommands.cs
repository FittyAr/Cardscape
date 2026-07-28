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

        bool alreadyVoted = await votes.HasVotedAsync(
            card.Id, currentUser.Id.Value, cancellationToken);
        if (alreadyVoted)
        {
            // Idempotent un-vote path: remove the user's row.
            IReadOnlyList<CardVote> existing = await votes.ListForCardAsync(
                card.Id, cancellationToken);
            CardVote? mine = existing.FirstOrDefault(v => v.UserId == currentUser.Id.Value);
            if (mine is not null)
            {
                votes.Remove(mine);
            }
        }
        else
        {
            var create = CardVote.Create(
                CardVoteId.New(), card.Id, currentUser.Id.Value, clock.UtcNow);
            if (create.IsFailure)
            {
                return Result.Failure<CardVoteStateDto>(create.Error);
            }

            await votes.AddAsync(create.Value, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        int count = await votes.CountForCardAsync(card.Id, cancellationToken);
        return Result.Success(new CardVoteStateDto(
            CardId: card.Id.Value,
            VoteCount: count,
            CurrentUserHasVoted: !alreadyVoted));
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
