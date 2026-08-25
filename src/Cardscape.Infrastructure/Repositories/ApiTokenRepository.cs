using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Security;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class ApiTokenRepository(CardscapeDbContext db)
    : RepositoryBase<ApiToken, ApiTokenId>(db), IApiTokenRepository
{
    public async Task<ApiToken?> FindByHashedSecretAsync(string hashedSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hashedSecret))
        {
            return null;
        }

        return await Db.Set<ApiToken>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.HashedSecret == hashedSecret, ct);
    }

    public async Task RecordUseAsync(
        ApiTokenId id,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        await Db.Set<ApiToken>()
            .Where(token => token.Id == id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.LastUsedAt, at)
                .SetProperty(token => token.UpdatedAt, at)
                .SetProperty(token => token.RowVersion, token => token.RowVersion + 1),
                ct);
    }

    public async Task<IReadOnlyList<ApiToken>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        IQueryable<ApiToken> query = Db.Set<ApiToken>()
            .AsNoTracking()
            .Where(token => token.UserId == new UserId(userId));
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderByDescending(token => token.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }
}
