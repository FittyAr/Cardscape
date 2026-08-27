using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITotpCredentialRepository"/>.
/// At most one credential per user (a unique index on
/// <c>UserId</c> enforces that).
/// </summary>
public sealed class TotpCredentialRepository(CardscapeDbContext db)
    : RepositoryBase<TotpCredential, TotpCredentialId>(db), ITotpCredentialRepository
{
    public async Task<TotpCredential?> FindForUserAsync(
        UserId userId,
        CancellationToken ct = default)
    {
        return await Set
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
    }

    public async Task<bool> AreActiveForAllUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken ct = default)
    {
        UserId[] distinctUserIds = userIds.Distinct().ToArray();
        if (distinctUserIds.Length == 0)
        {
            return true;
        }

        int activeCredentialCount = await Set
            .Where(credential =>
                credential.ConfirmedAt != null
                && !credential.IsDeleted
                && distinctUserIds.Contains(credential.UserId))
            .Select(credential => credential.UserId)
            .Distinct()
            .CountAsync(ct);

        return activeCredentialCount == distinctUserIds.Length;
    }
}
