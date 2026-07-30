namespace Cardscape.Domain.Integrations.GoogleDrive;

/// <summary>Identifier of a <see cref="GoogleDriveConnection"/>.</summary>
public sealed record GoogleDriveConnectionId(Guid Value) : Common.GuidId<GoogleDriveConnectionId>(Value);
