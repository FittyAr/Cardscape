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

public sealed record DownloadAttachmentQuery(Guid CardId, Guid AttachmentId) : IMessage;

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

        if (attachment.CardId.Value != query.CardId)
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
