using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Voting;

/// <summary>
/// One vote on a card. The unique <c>(CardId, UserId)</c>
/// constraint in the database is what prevents a single user
/// from voting twice; the domain object is a thin row whose
/// only behaviour is providing a factory. The list of voters
/// for a card is "all <see cref="CardVote"/> rows for that
/// card" and the count is just <c>card.Votes.Count</c>.
/// </summary>
public sealed class CardVote : Entity<CardVoteId>
{
    public CardId CardId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTimeOffset VotedAt { get; private set; }

    private CardVote() { }

    private CardVote(
        CardVoteId id,
        CardId cardId,
        Guid userId,
        DateTimeOffset at)
    {
        Id = id;
        CardId = cardId;
        UserId = userId;
        VotedAt = at;
        CreatedAt = at;
    }

    public static Result<CardVote> Create(
        CardVoteId id,
        CardId cardId,
        Guid userId,
        DateTimeOffset at)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<CardVote>(DomainError.Validation(
                "votes.user_required", "Vote owner is required."));
        }

        return Result.Success(new CardVote(id, cardId, userId, at));
    }
}
