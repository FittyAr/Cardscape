using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Voting;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CardVoteRepository(CardscapeDbContext db)
    : RepositoryBase<CardVote, CardVoteId>(db), ICardVoteRepository
{
    public async Task<int> CountForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        return await Set.CountAsync(vote => vote.CardId == cardId, ct);
    }

    public async Task<bool> HasVotedAsync(
        CardId cardId, Guid userId, CancellationToken ct = default)
    {
        return await Set
            .AnyAsync(vote => vote.CardId == cardId && vote.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<CardVote>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        IQueryable<CardVote> votes = Set
            .AsNoTracking()
            .Where(vote => vote.CardId == cardId);
        return await votes.ToListOrderedAsync(Db, vote => vote.VotedAt, ct: ct);
    }

    // BETA-3-#2 — see test-results/BETA-TEST-REPORT.md.
    //
    // Atomic DELETE-then-INSERT inside a single SQLite
    // transaction. The previous "read HasVotedAsync, branch,
    // save" pattern (still used by the read endpoint) was
    // vulnerable to a TOCTOU race when the same user toggled
    // their vote from two browser tabs at once: both calls
    // would observe HasVoted = false, both INSERT, the second
    // INSERT would violate the (CardId, UserId) unique index
    // and surface as 500 (now caught as 409 by
    // GlobalExceptionMiddleware, but the prior DTO was
    // computed from a stale pre-write snapshot). Wrapping the
    // DELETE + conditional INSERT in BeginTransaction makes
    // the pair atomic from SQLite's perspective and the
    // unique-index guarantee on the column is a belt-and-
    // braces safety net.
    public async Task<VoteToggleResult> ToggleAsync(
        CardId cardId,
        Guid userId,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be non-empty.", nameof(userId));
        }

        await using var tx = await Db.Database.BeginTransactionAsync(ct);

        CardVote? existing = await Set
            .FirstOrDefaultAsync(vote => vote.CardId == cardId && vote.UserId == userId, ct);

        if (existing is not null)
        {
            Set.Remove(existing);
        }
        else
        {
            var create = CardVote.Create(CardVoteId.New(), cardId, userId, at);
            if (create.IsFailure)
            {
                throw new InvalidOperationException(
                    $"ToggleAsync: factory failed with {create.Error.Code} — {create.Error.Message}");
            }
            await Set.AddAsync(create.Value, ct);
        }

        await Db.SaveChangesAsync(ct);

        // 2. Re-read the post-write state on a fresh query so
        //    the DTO reflects what actually committed. Without
        //    this re-read the handler would return a stale
        //    local-snapshot value (BETA-3-#2 root cause).
        int count = await CountForCardAsync(cardId, ct);
        bool nowVoted = existing is null; // null → we just inserted → now voted

        await tx.CommitAsync(ct);

        return new VoteToggleResult(nowVoted, count);
    }
}
