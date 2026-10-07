using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Common;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Lists.Commands;

/// <summary>
/// Trello-style "Copy list": creates a new list right after the source
/// and copies every non-archived card into it (see
/// <see cref="CardCopier"/> for what a card copy carries).
/// <see cref="Name"/> defaults to the source list name.
/// </summary>
public sealed record CopyListCommand(Guid ListId, string? Name) : IMessage;

public static class CopyListCommandHandler
{
    public static async Task<Result<BoardListDto>> HandleAsync(
        CopyListCommand command,
        IBoardRepository boards,
        IBoardListRepository lists,
        ICardRepository cards,
        IChecklistRepository checklists,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var guard = await MembershipGuards.EnsureCanMutateListAsync(
            lists, boards, currentUser.Id.Value, command.ListId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<BoardListDto>(guard.Error);
        }

        BoardList source = guard.Value.List;
        Result<ListName> name = string.IsNullOrWhiteSpace(command.Name)
            ? Result.Success(source.Name)
            : ListName.Create(command.Name);
        if (name.IsFailure)
        {
            return Result.Failure<BoardListDto>(name.Error);
        }

        // Slot the copy between the source and the next list, as Trello does.
        IReadOnlyList<BoardList> boardLists = await lists.ListForBoardAsync(
            source.BoardId, includeArchived: true, cancellationToken);
        BoardList? next = boardLists
            .Where(list => list.Id.Value != source.Id.Value && list.Position.Value > source.Position.Value)
            .MinBy(list => list.Position.Value);
        Position position = next is null
            ? Position.After(source.Position)
            : Position.Between(source.Position, next.Position);

        DateTimeOffset now = clock.UtcNow;
        Result<BoardList> created = BoardList.Create(
            BoardListId.New(), source.BoardId, name.Value, position, currentUser.Id.Value, now);
        if (created.IsFailure)
        {
            return Result.Failure<BoardListDto>(created.Error);
        }

        await lists.AddAsync(created.Value, cancellationToken);

        IReadOnlyList<Card> sourceCards = await cards.ListForListAsync(
            source.Id, includeArchived: false, cancellationToken);
        int copied = 0;
        foreach (Card card in sourceCards.OrderBy(c => c.Position.Value).ThenBy(c => c.CreatedAt))
        {
            Result<Card> copy = await CardCopier.CopyAsync(
                card, created.Value.Id, card.Title, card.Position, currentUser.Id.Value, now,
                cards, checklists, cancellationToken);
            if (copy.IsFailure)
            {
                return Result.Failure<BoardListDto>(copy.Error);
            }

            copied++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(BoardListDto.FromEntity(created.Value, copied));
    }
}
