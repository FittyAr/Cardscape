using Cardscape.Domain.Integrations.GoogleDrive;

namespace Cardscape.Application.Integrations.GoogleDrive.DTOs;

public sealed record GoogleDriveConnectionDto(
    Guid Id,
    Guid UserId,
    string GoogleEmail,
    DateTimeOffset? LastUsedAt,
    bool Active,
    DateTimeOffset CreatedAt)
{
    public static GoogleDriveConnectionDto FromEntity(GoogleDriveConnection c) => new(
        c.Id.Value,
        c.UserId.Value,
        c.GoogleEmail,
        c.LastUsedAt,
        c.Active,
        c.CreatedAt);
}
