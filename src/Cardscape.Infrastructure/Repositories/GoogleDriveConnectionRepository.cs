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
        GoogleDriveConnection? best = null;
        await foreach (var c in Db.Set<GoogleDriveConnection>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (c.UserId.Value == userValue && !c.IsDeleted)
            {
                if (best is null || c.CreatedAt > best.CreatedAt)
                {
                    best = c;
                }
            }
        }
        return best;
    }
}
