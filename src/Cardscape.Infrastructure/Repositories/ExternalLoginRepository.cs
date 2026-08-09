using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExternalLoginRepository"/>.
/// (Provider, Subject) is unique so the same external
/// identity can never be claimed by two Cardscape users.
/// </summary>
public sealed class ExternalLoginRepository(CardscapeDbContext db)
    : RepositoryBase<ExternalLogin, ExternalLoginId>(db), IExternalLoginRepository
{
    public async Task<ExternalLogin?> FindByProviderSubjectAsync(
        ExternalProvider provider,
        SubjectId subject,
        CancellationToken ct = default)
    {
        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(
                link => link.Provider == provider
                     && link.Subject == subject,
                ct);
    }

    public async Task<IReadOnlyList<ExternalLogin>> ListForUserAsync(
        UserId userId,
        CancellationToken ct = default)
    {
        return await Set
            .AsNoTracking()
            .Where(link => link.UserId == userId)
            .OrderByDescending(link => link.LastUsedAt)
            .ToListAsync(ct);
    }
}
