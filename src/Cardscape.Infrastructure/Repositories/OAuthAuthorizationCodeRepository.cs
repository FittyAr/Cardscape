using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class OAuthAuthorizationCodeRepository(CardscapeDbContext db)
    : RepositoryBase<OAuthAuthorizationCode, OAuthAuthorizationCodeId>(db),
      IOAuthAuthorizationCodeRepository
{
    public async Task<OAuthAuthorizationCode?> FindByCodeHashAsync(
        string codeHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            return null;
        }

        return await Db.Set<OAuthAuthorizationCode>()
            .FirstOrDefaultAsync(c => c.CodeHash == codeHash, ct);
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return await Db.Set<OAuthAuthorizationCode>()
            .Where(c => c.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
