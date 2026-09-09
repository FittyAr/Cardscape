using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Checklists;

public sealed record DeleteChecklistItemCommand(Guid ChecklistId, Guid ItemId) : IMessage;

public static class DeleteChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> HandleAsync(
        DeleteChecklistItemCommand command,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Result<ChecklistAccessContext> access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return Result.Failure<ChecklistDto>(access.Error);
        }

        Checklist checklist = access.Value.Checklist;
        var remove = checklist.RemoveItem(
            new ChecklistItemId(command.ItemId), clock.UtcNow);
        if (remove.IsFailure)
        {
            return Result.Failure<ChecklistDto>(remove.Error);
        }

        await activities.AddAsync(Activity.Create(
            access.Value.BoardId,
            access.Value.Card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.ChecklistCreated,
            $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{command.ItemId}\",\"action\":\"delete\"}}",
            clock.UtcNow), ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}
