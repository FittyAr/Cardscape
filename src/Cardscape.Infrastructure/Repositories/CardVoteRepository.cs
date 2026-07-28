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
}
