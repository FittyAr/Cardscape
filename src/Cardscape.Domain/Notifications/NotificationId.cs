namespace Cardscape.Domain.Notifications;

/// <summary>Identifier of a notification.</summary>
public sealed record NotificationId(Guid Value) : Common.GuidId<NotificationId>(Value);
