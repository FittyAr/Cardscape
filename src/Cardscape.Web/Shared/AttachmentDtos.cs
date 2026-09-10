namespace Cardscape.Web.Shared;

// ── Attachments (BUG-A5-002) ────────────────────────────
public sealed record AttachmentDto(
    Guid Id,
    Guid CardId,
    string FileName,
    string MimeType,
    long SizeBytes,
    Guid UploaderId,
    DateTimeOffset CreatedAt);
