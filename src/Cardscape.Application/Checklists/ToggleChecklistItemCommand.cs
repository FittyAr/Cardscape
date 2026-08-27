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

public sealed record ToggleChecklistItemCommand(Guid ChecklistId, Guid ItemId) : IMessage;

public static class ToggleChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        ToggleChecklistItemCommand command,
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
        ChecklistItem? item = checklist.Items.FirstOrDefault(
            i => i.Id.Value == command.ItemId);
        if (item is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.NotFound(
                "checklist_items.not_found", "Item was not found."));
        }

        var result = item.IsCompleted
            ? checklist.UncheckItem(new ChecklistItemId(command.ItemId), clock.UtcNow)
            : checklist.CheckItem(new ChecklistItemId(command.ItemId), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<ChecklistDto>(result.Error);
        }

        ActivityKind kind = item.IsCompleted
            ? ActivityKind.ChecklistItemCompleted
            : ActivityKind.ChecklistItemUncompleted;
        await activities.AddAsync(Activity.Create(
            access.Value.BoardId,
            access.Value.Card.Id.Value,
            currentUser.Id.Value,
            kind,
            $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{item.Id.Value}\"}}",
            clock.UtcNow), ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}
