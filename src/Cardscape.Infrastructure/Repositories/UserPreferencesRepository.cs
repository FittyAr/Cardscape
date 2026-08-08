using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IUserPreferencesRepository"/>.
/// The aggregate is keyed by <see cref="UserId"/>; the standard
/// <c>GetByIdAsync</c> from <see cref="RepositoryBase{TEntity, TId}"/>
/// works unchanged. The GDPR cleanup path uses
/// <see cref="DeleteByUserIdAsync"/> so the user-lifecycle
/// command handlers do not have to do a prior read.</summary>
public sealed class UserPreferencesRepository(CardscapeDbContext db)
    : RepositoryBase<UserPreferences, UserId>(db), IUserPreferencesRepository
{
    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        // Stream client-side: the strongly-typed-id conversion
        // does not translate to SQL with the current EF Core
        // 10 + Npgsql / SQLite / MySql provider matrix. The
        // table is at most one row per user, so the cost is
        // trivial.
        await foreach (UserPreferences prefs in Db.Set<UserPreferences>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (prefs.Id.Value == userId)
            {
                Db.Set<UserPreferences>().Remove(prefs);
                return;
            }
        }
    }
}
