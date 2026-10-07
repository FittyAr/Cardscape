using Cardscape.Domain.Attachments;

namespace Cardscape.Application.Attachments;

public sealed record AttachmentDto(
    Guid Id,
    Guid CardId,
    string FileName,
    string MimeType,
    long SizeBytes,
    Guid UploaderId,
    DateTimeOffset CreatedAt)
{
    public static AttachmentDto FromEntity(Attachment a) => new(
        a.Id.Value,
        a.CardId.Value,
        a.FileName,
        a.MimeType,
        a.SizeBytes,
        a.UploaderId,
        a.CreatedAt);
}
