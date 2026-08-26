using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Queries;

public sealed record GetCardQuery(Guid CardId) : IMessage;

public static class GetCardQueryHandler
{
    public static async Task<Result<CardDto>> Handle(
        GetCardQuery query,
        ICardRepository cards,
        ICardSnoozeRepository snoozes,
        ICardMirrorRepository mirrors,
        ICommentRepository comments,
        IChecklistRepository checklists,
        IAttachmentRepository attachments,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanReadCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        CardSnooze? snooze = await snoozes.GetByCardIdAsync(card.Id, cancellationToken);
        CardMirror? mirror = await mirrors.GetByMirroredCardIdAsync(card.Id, cancellationToken);
        int commentCount = await comments.CountForCardAsync(card.Id, cancellationToken);
        int attachmentCount = await attachments.CountForCardAsync(card.Id.Value, cancellationToken);
        int checklistCount = await checklists.CountForCardAsync(card.Id.Value, cancellationToken);

        CardDto baseDto = card.MapToDto(snooze, clock.UtcNow, mirror?.SourceCardId.Value);
        return Result.Success(baseDto with
        {
            CommentCount = commentCount,
            AttachmentCount = attachmentCount,
            ChecklistCount = checklistCount
        });
    }
}
