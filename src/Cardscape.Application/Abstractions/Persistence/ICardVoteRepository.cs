using Cardscape.Domain.Cards;
using Cardscape.Domain.Voting;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardVoteRepository : IRepository<CardVote, CardVoteId>
{
    /// <summary>Number of votes the card has received.</summary>
    Task<int> CountForCardAsync(CardId cardId, CancellationToken ct = default);

    /// <summary>True if the user has already voted on the card.</summary>
    Task<bool> HasVotedAsync(CardId cardId, Guid userId, CancellationToken ct = default);

    /// <summary>All voters for a card (one row per voter).</summary>
    Task<IReadOnlyList<CardVote>> ListForCardAsync(CardId cardId, CancellationToken ct = default);
}
