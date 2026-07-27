namespace Cardscape.Domain.Attachments;

/// <summary>Identifier of an attachment.</summary>
public sealed record AttachmentId(Guid Value) : Common.GuidId<AttachmentId>(Value);
