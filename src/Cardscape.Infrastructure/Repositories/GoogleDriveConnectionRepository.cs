using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.GoogleDrive;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class GoogleDriveConnectionRepository(CardscapeDbContext db)
    : RepositoryBase<GoogleDriveConnection, GoogleDriveConnectionId>(db), IGoogleDriveConnectionRepository
{
    public async Task<GoogleDriveConnection?> FindForUserAsync(
        UserId userId, CancellationToken ct = default)
    {
        var userValue = userId.Value;
        return await Task.Run<GoogleDriveConnection?>(() =>
        {
            return Db.Set<GoogleDriveConnection>().AsEnumerable()
                .Where(c => c.UserId.Value == userValue && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault();
        }, ct);
    }
}
