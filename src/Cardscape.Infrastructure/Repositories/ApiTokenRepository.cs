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

        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.HashedSecret == hashedSecret, ct);
    }

    public async Task RecordUseAsync(
        ApiTokenId id,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        await Set
            .Where(token => token.Id == id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.LastUsedAt, at)
                .SetProperty(token => token.UpdatedAt, at)
                .SetProperty(token => token.RowVersion, token => token.RowVersion + 1),
                ct);
    }

    public async Task<IReadOnlyList<ApiToken>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        IQueryable<ApiToken> query = Set
            .AsNoTracking()
            .Where(token => token.UserId == new UserId(userId));
        return await query.ToListOrderedAsync(Db, token => token.CreatedAt, descending: true, ct: ct);
    }
}
