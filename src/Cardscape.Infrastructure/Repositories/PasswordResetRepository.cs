using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Infrastructure.Persistence;



namespace Cardscape.Infrastructure.Repositories;

public sealed class PasswordResetRepository(CardscapeDbContext db)
    : RepositoryBase<PasswordReset, PasswordResetId>(db), IPasswordResetRepository
{
    public async Task<PasswordReset?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        PasswordReset? found = null;
        await foreach (PasswordReset r in Db.Set<PasswordReset>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (string.Equals(r.TokenHash, tokenHash, StringComparison.Ordinal) && !r.IsDeleted)
            {
                found = r;
                break;
            }
        }
        return found;
    }
}
