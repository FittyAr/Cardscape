using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Common;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

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

public sealed record ListCardAttachmentsQuery(Guid CardId) : IMessage;

public static class ListCardAttachmentsQueryHandler
{
    public static async Task<Result<IReadOnlyList<AttachmentDto>>> Handle(
        ListCardAttachmentsQuery query,
        IAttachmentRepository attachments,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<AttachmentDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(query.CardId), ct);
        if (card is null)
        {
            return Result.Failure<IReadOnlyList<AttachmentDto>>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        // Reuse the existing read-guard; the user can read attachments
        // if they can read the card.
        var readGuard = await MembershipGuards.EnsureCanReadCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (readGuard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AttachmentDto>>(readGuard.Error);
        }

        IReadOnlyList<Attachment> rows = await attachments.ListForCardAsync(query.CardId, ct);
        return Result.Success<IReadOnlyList<AttachmentDto>>(
            rows.Select(AttachmentDto.FromEntity).ToList());
    }
}

public sealed record UploadAttachmentCommand(
    Guid CardId,
    string FileName,
    string MimeType,
    long SizeBytes,
    Stream Content) : IMessage;

public static class UploadAttachmentCommandHandler
{
    public static async Task<Result<AttachmentDto>> Handle(
        UploadAttachmentCommand command,
        IAttachmentRepository attachments,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        IStorageService storage,
        IClock clock,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<AttachmentDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure<AttachmentDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (guard.IsFailure)
        {
            return Result.Failure<AttachmentDto>(guard.Error);
        }

        if (command.SizeBytes < 0)
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.size_invalid", "File size cannot be negative."));
        }

        // 25 MB hard cap — matches ASP.NET's default request body
        // budget and keeps the in-memory stream bounded for
        // LocalFileStorageService.
        const long MaxBytes = 25L * 1024L * 1024L;
        if (command.SizeBytes > MaxBytes)
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.too_large", $"File exceeds the {MaxBytes / (1024 * 1024)} MB cap."));
        }

        string storageKey = $"cards/{command.CardId:N}/{Guid.NewGuid():N}/{command.FileName}";
        await storage.SaveAsync(storageKey, command.Content, command.MimeType, ct);

        var creation = Attachment.Create(
            AttachmentId.New(),
            new CardId(command.CardId),
            command.FileName,
            string.IsNullOrWhiteSpace(command.MimeType) ? "application/octet-stream" : command.MimeType,
            command.SizeBytes,
            storageKey,
            currentUser.Id.Value,
            clock.UtcNow);

        if (creation.IsFailure)
        {
            return Result.Failure<AttachmentDto>(creation.Error);
        }

        await attachments.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(AttachmentDto.FromEntity(creation.Value));
    }
}

public sealed record DownloadAttachmentQuery(Guid AttachmentId) : IMessage;

public sealed record AttachmentDownload(
    string FileName,
    string MimeType,
    Stream Content,
    long SizeBytes);

public static class DownloadAttachmentQueryHandler
{
    public static async Task<Result<AttachmentDownload>> Handle(
        DownloadAttachmentQuery query,
        IAttachmentRepository attachments,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IStorageService storage,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<AttachmentDownload>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var attachment = await attachments.GetByIdAsync(new AttachmentId(query.AttachmentId), ct);
        if (attachment is null)
        {
            return Result.Failure<AttachmentDownload>(DomainError.NotFound(
                "attachments.not_found", "Attachment was not found."));
        }

        var card = await cards.GetByIdAsync(attachment.CardId, ct);
        if (card is null)
        {
            return Result.Failure<AttachmentDownload>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var readGuard = await MembershipGuards.EnsureCanReadCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (readGuard.IsFailure)
        {
            return Result.Failure<AttachmentDownload>(readGuard.Error);
        }

        Stream stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return Result.Success(new AttachmentDownload(
            attachment.FileName,
            attachment.MimeType,
            stream,
            attachment.SizeBytes));
    }
}

public sealed record DeleteAttachmentCommand(Guid AttachmentId) : IMessage;

public static class DeleteAttachmentCommandHandler
{
    public static async Task<Result<bool>> Handle(
        DeleteAttachmentCommand command,
        IAttachmentRepository attachments,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        IStorageService storage,
        IClock clock,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<bool>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var attachment = await attachments.GetByIdAsync(new AttachmentId(command.AttachmentId), ct);
        if (attachment is null)
        {
            return Result.Failure<bool>(DomainError.NotFound(
                "attachments.not_found", "Attachment was not found."));
        }

        var card = await cards.GetByIdAsync(attachment.CardId, ct);
        if (card is null)
        {
            return Result.Failure<bool>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (guard.IsFailure)
        {
            return Result.Failure<bool>(guard.Error);
        }

        string storageKey = attachment.StorageKey;
        attachments.Remove(attachment);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            await storage.DeleteAsync(storageKey, ct);
        }
        catch
        {
            // Best-effort cleanup; the metadata row is already gone
            // and a stale blob is harmless next to the row.
        }

        _ = clock.UtcNow; // surface dependency
        return Result.Success(true);
    }
}
