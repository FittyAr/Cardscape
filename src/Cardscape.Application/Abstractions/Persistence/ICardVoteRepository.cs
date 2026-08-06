using Cardscape.Domain.Cards;
using Cardscape.Domain.Voting;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Result of a single atomic vote toggle.</summary>
/// <param name="NowVoted">True if the user has a vote row AFTER the toggle.</param>
/// <param name="VoteCount">The new total vote count on the card.</param>
public sealed record VoteToggleResult(bool NowVoted, int VoteCount);

public interface ICardVoteRepository : IRepository<CardVote, CardVoteId>
{
    /// <summary>Number of votes the card has received.</summary>
    Task<int> CountForCardAsync(CardId cardId, CancellationToken ct = default);

    /// <summary>True if the user has already voted on the card.</summary>
    Task<bool> HasVotedAsync(CardId cardId, Guid userId, CancellationToken ct = default);

    /// <summary>All voters for a card (one row per voter).</summary>
    Task<IReadOnlyList<CardVote>> ListForCardAsync(CardId cardId, CancellationToken ct = default);

    /// <summary>
    /// Atomically toggles the caller's vote on a card: deletes the
    /// existing <c>card_votes</c> row if present (i.e. caller was
    /// voting, now un-votes), or inserts a new row if absent (caller
    /// wasn't voting, now votes). The DELETE + INSERT is wrapped in
    /// a single transaction so two concurrent calls on the same
    /// <c>(CardId, UserId)</c> pair can't both observe a "no row
    /// present" state and both INSERT (which would violate the
    /// <c>(CardId, UserId)</c> unique index and surface as 500).
    ///
    /// This is BETA-3-#2 — see test-results/BETA-TEST-REPORT.md.
    /// The previous implementation read <c>HasVotedAsync</c>,
    /// branched, and saved. Two concurrent calls could both
    /// observe "not voted" and both INSERT, the second INSERT
    /// failing with a unique-index violation. The DTO that the
    /// handler returned (<c>CurrentUserHasVoted</c>) was computed
    /// from the pre-write local variable, so the caller could
    /// see <c>CurrentUserHasVoted = true</c> even after a 409.
    /// </summary>
    Task<VoteToggleResult> ToggleAsync(
        CardId cardId,
        Guid userId,
        DateTimeOffset at,
        CancellationToken ct = default);
}
