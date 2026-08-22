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
        UserPreferences? preferences = await Db.Set<UserPreferences>()
            .FirstOrDefaultAsync(prefs => prefs.Id == new UserId(userId), ct);
        if (preferences is not null)
        {
            Db.Set<UserPreferences>().Remove(preferences);
        }
    }
}
