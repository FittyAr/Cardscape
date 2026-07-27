using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Attachments;

/// <summary>
/// A file attached to a card. The actual binary lives in
/// <see cref="StorageKey"/>, which is interpreted by the
/// <c>IStorageService</c> in the application layer.
/// </summary>
public sealed class Attachment : AggregateRoot<AttachmentId>
{
    public CardId CardId { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string MimeType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public Guid UploaderId { get; private set; }

    private Attachment() { }

    private Attachment(
        AttachmentId id,
        CardId cardId,
        string fileName,
        string mimeType,
        long sizeBytes,
        string storageKey,
        Guid uploaderId,
        DateTimeOffset at)
    {
        Id = id;
        CardId = cardId;
        FileName = fileName;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploaderId = uploaderId;
        CreatedAt = at;
    }

    public static Result<Attachment> Create(
        AttachmentId id,
        CardId cardId,
        string fileName,
        string mimeType,
        long sizeBytes,
        string storageKey,
        Guid uploaderId,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result.Failure<Attachment>(DomainError.Validation(
                "attachments.file_name_required",
                "File name is required."));
        }

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return Result.Failure<Attachment>(DomainError.Validation(
                "attachments.mime_type_required",
                "Mime type is required."));
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Result.Failure<Attachment>(DomainError.Validation(
                "attachments.storage_key_required",
                "Storage key is required."));
        }

        if (sizeBytes < 0)
        {
            return Result.Failure<Attachment>(DomainError.Validation(
                "attachments.size_invalid",
                "File size cannot be negative."));
        }

        if (uploaderId == Guid.Empty)
        {
            return Result.Failure<Attachment>(DomainError.Validation(
                "attachments.uploader_required",
                "Uploader is required."));
        }

        var attachment = new Attachment(id, cardId, fileName, mimeType, sizeBytes, storageKey, uploaderId, at);
        return Result.Success(attachment);
    }
}
