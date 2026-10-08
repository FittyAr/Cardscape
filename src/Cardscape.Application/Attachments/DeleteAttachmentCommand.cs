using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Attachments;

public sealed record DeleteAttachmentCommand(Guid CardId, Guid AttachmentId) : IMessage;

public static class DeleteAttachmentCommandHandler
{
    public static async Task<Result<bool>> HandleAsync(
        DeleteAttachmentCommand command,
        IAttachmentRepository attachments,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        IStorageService storage,
        IClock clock,
        ICurrentUser currentUser,
        IActivityRepository activities,
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

        if (attachment.CardId.Value != command.CardId)
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
        await activities.AddAsync(Activity.Record(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.AttachmentRemoved,
            clock.UtcNow,
            new { attachmentId = attachment.Id.Value, fileName = attachment.FileName }), ct);
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

        return Result.Success(true);
    }
}
