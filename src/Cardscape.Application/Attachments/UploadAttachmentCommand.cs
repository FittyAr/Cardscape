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

        // BETA-A5-R2-004 + BETA-A5-R2-005 — see
        // test-results/beta/round-2/reports/A5-card-extras.md.
        //
        // Two security holes in the previous handler:
        //   1. The MIME type was taken verbatim from the client
        //      with no validation. A `.exe` uploaded as
        //      `application/x-msdownload` would be served back
        //      to the user's browser with the right
        //      Content-Type, triggering a download / execution
        //      prompt. The fix is a denylist of dangerous
        //      MIME types — executables, scripts, and archives
        //      that can be served back with a content-
        //      disposition.
        //   2. The `FileName` was embedded verbatim into the
        //      storage key, so `../../etc/passwd` would have
        //      escaped the `/app/Storage` root on the local
        //      file storage backend. The fix is to compute a
        //      safe basename and the storage key from a fresh
        //      GUID, with the user-supplied filename kept only
        //      as a metadata field.
        string mimeType = AttachmentUploadPolicy.NormalizeMimeType(command.MimeType);
        if (AttachmentUploadPolicy.IsBlockedMimeType(mimeType))
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.mime_blocked",
                $"MIME type '{mimeType}' is not allowed for attachments."));
        }

        string safeName = AttachmentUploadPolicy.SanitizeFileName(command.FileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return Result.Failure<AttachmentDto>(DomainError.Validation(
                "attachments.name_invalid", "File name is required and must contain at least one allowed character."));
        }

        string storageKey = $"cards/{command.CardId:N}/{Guid.NewGuid():N}/{safeName}";
        var creation = Attachment.Create(
            AttachmentId.New(),
            new CardId(command.CardId),
            safeName,
            mimeType,
            command.SizeBytes,
            storageKey,
            currentUser.Id.Value,
            clock.UtcNow);

        if (creation.IsFailure)
        {
            return Result.Failure<AttachmentDto>(creation.Error);
        }

        try
        {
            await storage.SaveAsync(storageKey, command.Content, mimeType, ct);
            await attachments.AddAsync(creation.Value, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            try
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch
            {
                // Preserve the original failure. The storage key is unique,
                // so a later retention sweep can safely remove this orphan.
            }

            throw;
        }

        return Result.Success(AttachmentDto.FromEntity(creation.Value));
    }
}
