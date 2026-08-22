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
