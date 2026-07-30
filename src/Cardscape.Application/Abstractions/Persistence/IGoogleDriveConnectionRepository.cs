using Cardscape.Domain.Integrations.GoogleDrive;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="GoogleDriveConnection"/>.</summary>
public interface IGoogleDriveConnectionRepository : IRepository<GoogleDriveConnection, GoogleDriveConnectionId>
{
    /// <summary>Loads the active connection for a user. There is
    /// at most one active <see cref="GoogleDriveConnection"/>
    /// per <see cref="UserId"/> in v1.</summary>
    Task<GoogleDriveConnection?> FindForUserAsync(
        UserId userId, CancellationToken ct = default);
}
