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

        // v1.2.0 audit (pass 12): IDOR — see
        // ChecklistsAccess.EnsureCanAccessChecklistAsync.
        var access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return Result.Failure<ChecklistItemDto>(access.Error);
        }

        Checklist? checklist = await checklists.GetByIdAsync(
            new ChecklistId(command.ChecklistId), ct);
        if (checklist is null)
        {
            return Result.Failure<ChecklistItemDto>(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        Position position = Position.From(checklist.Items.Count + 1);
        checklist.AddItem(textResult.Value, position, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        // BETA-7-#1 / #2 — index the new item and record the
        // addition on the activity feed. The new item is the
        // last one in the checklist (highest position).
        ChecklistItem? newItem = checklist.Items.OrderByDescending(i => i.Position.Value).FirstOrDefault();
        if (newItem is null)
        {
            return Result.Failure<ChecklistItemDto>(DomainError.Conflict(
                "checklists.item_not_added", "Checklist item could not be added."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (card is not null && map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(boardId),
                card.Id.Value,
                currentUser.Id.Value,
                ActivityKind.ChecklistCreated, // ChecklistItemAdded reuses ChecklistCreated until a dedicated kind is added.
                $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{newItem.Id.Value}\"}}",
                clock.UtcNow), ct);
            await uow.SaveChangesAsync(ct);
        }

        // BETA-8-API-#3 — return the created item alone. The
        // previous shape returned the whole ChecklistDto, which
        // forced the client to diff/replace a list it already
        // had in memory just to learn the new item id. We
        // hand the caller the one row it actually needs.
        ChecklistItemDto dto = new(
            newItem.Id.Value,
            newItem.ChecklistId.Value,
            newItem.Text.Value,
            newItem.IsCompleted,
            (int)newItem.Position.Value,
            newItem.AssignedTo);
        return Result.Success(dto);
    }
}

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

        // v1.2.0 audit (pass 12): IDOR — see
        // ChecklistsAccess.EnsureCanAccessChecklistAsync.
        var access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return Result.Failure<ChecklistDto>(access.Error);
        }

        Checklist? checklist = await checklists.GetByIdAsync(
            new ChecklistId(command.ChecklistId), ct);
        if (checklist is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        var update = checklist.UpdateItem(
            new ChecklistItemId(command.ItemId), textResult.Value, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<ChecklistDto>(update.Error);
        }

        await uow.SaveChangesAsync(ct);

        // BETA-7-#1 / #2 — re-index the renamed item.
        ChecklistItem? updated = checklist.Items.FirstOrDefault(i => i.Id.Value == command.ItemId);
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (updated is not null && card is not null && map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(boardId),
                card.Id.Value,
                currentUser.Id.Value,
                ActivityKind.ChecklistCreated, // ChecklistItemRenamed reuses ChecklistCreated until a dedicated kind is added.
                $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{updated.Id.Value}\"}}",
                clock.UtcNow), ct);
            await uow.SaveChangesAsync(ct);
        }

        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}
