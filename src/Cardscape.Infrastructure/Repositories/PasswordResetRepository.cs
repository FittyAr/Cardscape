using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class PasswordResetRepository(CardscapeDbContext db)
    : RepositoryBase<PasswordReset, PasswordResetId>(db), IPasswordResetRepository
{
    public async Task<PasswordReset?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return null;
        return await Db.Set<PasswordReset>()
            .FirstOrDefaultAsync(reset => reset.TokenHash == tokenHash && !reset.IsDeleted, ct);
    }
}
