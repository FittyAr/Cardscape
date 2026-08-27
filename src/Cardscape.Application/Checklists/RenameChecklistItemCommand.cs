using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Checklists;

public sealed record RenameChecklistItemCommand(Guid ChecklistId, Guid ItemId, string Text) : IMessage;

public static class RenameChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        RenameChecklistItemCommand command,
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

        var textResult = ChecklistItemText.Create(command.Text);
        if (textResult.IsFailure)
        {
            return Result.Failure<ChecklistDto>(textResult.Error);
        }

        Result<ChecklistAccessContext> access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return Result.Failure<ChecklistDto>(access.Error);
        }

        Checklist checklist = access.Value.Checklist;
        var update = checklist.UpdateItem(
            new ChecklistItemId(command.ItemId), textResult.Value, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<ChecklistDto>(update.Error);
        }

        await activities.AddAsync(Activity.Create(
            access.Value.BoardId,
            access.Value.Card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.ChecklistCreated,
            $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{command.ItemId}\"}}",
            clock.UtcNow), ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}
