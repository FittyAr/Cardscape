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
            .FirstOrDefaultAsync(t => t.HashedSecret == hashedSecret, ct);
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
