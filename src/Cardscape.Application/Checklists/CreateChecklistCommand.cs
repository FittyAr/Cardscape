using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Checklists;

public sealed record CreateChecklistCommand(Guid CardId, string Title) : IMessage;

public static class CreateChecklistCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        CreateChecklistCommand command,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IUnitOfWork uow,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var titleResult = ChecklistTitle.Create(command.Title);
        if (titleResult.IsFailure)
        {
            return Result.Failure<ChecklistDto>(titleResult.Error);
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        BoardId? boardId = await lists.GetBoardIdAsync(card.ListId, ct);
        if (boardId is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(boardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<ChecklistDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var create = Checklist.Create(
            ChecklistId.New(), card.Id, titleResult.Value, currentUser.Id.Value, clock.UtcNow);
        if (create.IsFailure)
        {
            return Result.Failure<ChecklistDto>(create.Error);
        }

        await checklists.AddAsync(create.Value, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success(ChecklistDto.FromEntity(create.Value));
    }
}
