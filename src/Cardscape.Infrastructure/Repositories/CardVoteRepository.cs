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
        var cardIdValue = cardId.Value;
        // HasConversion on the CardId value-object means EF can't
        // translate the navigation in the WHERE clause; do the
        // filter client-side. The result set is bounded by the
        // vote count on a single card.
        return await Task.Run(() =>
        {
            return Db.Set<CardVote>().AsEnumerable()
                .Count(v => v.CardId.Value == cardIdValue);
        }, ct);
    }

    public async Task<bool> HasVotedAsync(
        CardId cardId, Guid userId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        return await Task.Run(() =>
        {
            return Db.Set<CardVote>().AsEnumerable()
                .Any(v => v.CardId.Value == cardIdValue && v.UserId == userId);
        }, ct);
    }

    public async Task<IReadOnlyList<CardVote>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        return await Task.Run(() =>
        {
            return Db.Set<CardVote>().AsEnumerable()
                .Where(v => v.CardId.Value == cardIdValue)
                .OrderBy(v => v.VotedAt)
                .ToList();
        }, ct);
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

        var cardIdValue = cardId.Value;
        await using var tx = await Db.Database.BeginTransactionAsync(ct);

        // 1. Async-stream the votes for this card and find the
        //    (CardId, UserId) pair. AsAsyncEnumerable yields
        //    rows without parking a thread-pool worker.
        CardVote? existing = null;
        await foreach (var v in Db.Set<CardVote>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (v.CardId.Value == cardIdValue && v.UserId == userId)
            {
                existing = v;
                break;
            }
        }

        if (existing is not null)
        {
            Db.Set<CardVote>().Remove(existing);
        }
        else
        {
            var create = CardVote.Create(CardVoteId.New(), cardId, userId, at);
            if (create.IsFailure)
            {
                throw new InvalidOperationException(
                    $"ToggleAsync: factory failed with {create.Error.Code} — {create.Error.Message}");
            }
            await Db.Set<CardVote>().AddAsync(create.Value, ct);
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
