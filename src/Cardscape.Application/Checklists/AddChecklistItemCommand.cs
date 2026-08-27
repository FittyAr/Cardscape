using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Checklists;

public sealed record AddChecklistItemCommand(Guid ChecklistId, string Text) : IMessage;

public static class AddChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistItemDto>> Handle(
        AddChecklistItemCommand command,
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
            return Result.Failure<ChecklistItemDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var textResult = ChecklistItemText.Create(command.Text);
        if (textResult.IsFailure)
        {
            return Result.Failure<ChecklistItemDto>(textResult.Error);
        }

        Result<ChecklistAccessContext> access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return Result.Failure<ChecklistItemDto>(access.Error);
        }

        Checklist checklist = access.Value.Checklist;
        Position position = Position.From(checklist.Items.Count + 1);
        ChecklistItem newItem = checklist.AddItem(textResult.Value, position, clock.UtcNow);

        await activities.AddAsync(Activity.Create(
            access.Value.BoardId,
            access.Value.Card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.ChecklistCreated,
            $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{newItem.Id.Value}\"}}",
            clock.UtcNow), ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new ChecklistItemDto(
            newItem.Id.Value,
            newItem.ChecklistId.Value,
            newItem.Text.Value,
            newItem.IsCompleted,
            (int)newItem.Position.Value,
            newItem.AssignedTo));
    }
}
