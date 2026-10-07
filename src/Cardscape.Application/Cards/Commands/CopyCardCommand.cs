using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;

namespace Cardscape.Application.Cards.Commands;

/// <summary>
/// Trello-style "Copy card": duplicates a card (title, description,
/// cover, labels, members, checklists) into a list of the same board.
/// <see cref="Title"/> defaults to the source title and
/// <see cref="Position"/> to the bottom of the target list.
/// </summary>
public sealed record CopyCardCommand(Guid CardId, Guid TargetListId, string? Title, double? Position)
    : IMessage;

public static class CopyCardCommandHandler
{
    public static async Task<Result<CardDto>> HandleAsync(
        CopyCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IChecklistRepository checklists,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? source = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (source is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            source, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        // Labels are board-scoped, so a copy stays on the source board.
        BoardList? target = await lists.GetByIdAsync(new BoardListId(command.TargetListId), cancellationToken);
        if (target is null || target.BoardId.Value != guard.Value.Board.Id.Value)
        {
            return Result.Failure<CardDto>(DomainError.Validation(
                "cards.invalid_copy",
                "Destination list must belong to the same board as the card."));
        }

        if (target.IsArchived)
        {
            return Result.Failure<CardDto>(DomainError.Validation(
                "cards.copy_to_archived_list",
                "Cards cannot be copied into an archived list."));
        }

        Result<CardTitle> title = string.IsNullOrWhiteSpace(command.Title)
            ? Result.Success(source.Title)
            : CardTitle.Create(command.Title);
        if (title.IsFailure)
        {
            return Result.Failure<CardDto>(title.Error);
        }

        Position position;
        if (command.Position is { } requested)
        {
            position = Position.From(requested);
        }
        else
        {
            IReadOnlyList<Card> siblings = await cards.ListForListAsync(
                target.Id, includeArchived: true, cancellationToken);
            position = siblings.Count == 0
                ? Position.Start()
                : Position.After(Position.From(siblings.Max(sibling => sibling.Position.Value)));
        }

        Result<Card> copy = await CardCopier.CopyAsync(
            source, target.Id, title.Value, position, currentUser.Id.Value, clock.UtcNow,
            cards, checklists, cancellationToken);
        if (copy.IsFailure)
        {
            return Result.Failure<CardDto>(copy.Error);
        }

        await activities.AddAsync(Activity.Create(
            target.BoardId,
            copy.Value.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardCreated,
            $"{{\"title\":\"{copy.Value.Title.Value.Replace("\"", "\\\"")}\",\"copiedFrom\":\"{source.Id.Value}\"}}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(copy.Value.MapToDto());
    }
}
