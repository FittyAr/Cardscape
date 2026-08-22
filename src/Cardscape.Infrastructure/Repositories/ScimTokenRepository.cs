using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class ScimTokenRepository(CardscapeDbContext db) : IScimTokenRepository
{
    public Task<ScimToken?> FindByIdAsync(ScimTokenId id, CancellationToken ct = default) =>
        db.ScimTokens.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<ScimToken?> FindByPlaintextAsync(string plaintext, CancellationToken ct = default)
    {
        // Salted password-style hashes cannot be queried by equality. Filter
        // revoked rows in SQL, then verify the remaining candidates locally.
        IQueryable<ScimToken> active = db.ScimTokens.Where(token => !token.IsRevoked);
        await foreach (ScimToken token in active.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (token.Verify(plaintext))
            {
                return token;
            }
        }
        return null;
    }

    public async Task<IReadOnlyList<ScimToken>> ListForWorkspaceAsync(Guid workspaceId, CancellationToken ct = default)
    {
        // SQLite does not support ORDER BY on DateTimeOffset
        // columns (the engine's ORDER BY only handles a fixed
        // set of primitive types). The list is small (typically
        // 1-2 tokens per workspace) so we fetch the matching
        // rows in any order and sort client-side.
        IQueryable<ScimToken> query = db.ScimTokens
            .AsNoTracking()
            .Where(token => token.WorkspaceId == new Domain.Workspaces.WorkspaceId(workspaceId));
        if (!db.Database.IsSqlite())
        {
            return await query.OrderByDescending(token => token.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }

    public async Task AddAsync(ScimToken token, CancellationToken ct = default)
    {
        await db.ScimTokens.AddAsync(token, ct);
    }
}
