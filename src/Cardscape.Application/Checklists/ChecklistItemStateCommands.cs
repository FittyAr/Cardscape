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

        await uow.SaveChangesAsync(ct);

        // BETA-7-#2 — record the toggle on the activity feed.
        // Check / uncheck use the dedicated ChecklistItem*
        // kinds; the existing completed flag tells us which.
        ActivityKind kind = item.IsCompleted
            ? ActivityKind.ChecklistItemCompleted
            : ActivityKind.ChecklistItemUncompleted;
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (card is not null && map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(boardId),
                card.Id.Value,
                currentUser.Id.Value,
                kind,
                $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{item.Id.Value}\"}}",
                clock.UtcNow), ct);
            await uow.SaveChangesAsync(ct);
        }

        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

public sealed record DeleteChecklistItemCommand(Guid ChecklistId, Guid ItemId) : IMessage;

public static class DeleteChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
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

        var remove = checklist.RemoveItem(
            new ChecklistItemId(command.ItemId), clock.UtcNow);
        if (remove.IsFailure)
        {
            return Result.Failure<ChecklistDto>(remove.Error);
        }

        await uow.SaveChangesAsync(ct);

        // BETA-7-#2 — record the deletion on the activity
        // feed. There is no dedicated ChecklistItemDeleted
        // kind, so we reuse ChecklistCreated.
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (card is not null && map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            await activities.AddAsync(Activity.Create(
                new Domain.Boards.BoardId(boardId),
                card.Id.Value,
                currentUser.Id.Value,
                ActivityKind.ChecklistCreated,
                $"{{\"checklistId\":\"{checklist.Id.Value}\",\"itemId\":\"{command.ItemId}\",\"action\":\"delete\"}}",
                clock.UtcNow), ct);
            await uow.SaveChangesAsync(ct);
        }

        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

/// <summary>Internal helper that wraps the membership-check pattern
/// shared by all per-checklist operations.</summary>
internal static class ChecklistsAccess
{
    /// <summary>
    /// Loads the checklist, follows card→list→board, and verifies
    /// the current user is a board member. The v1.2.0 audit
    /// (pass 12) found that the rename / delete / item handlers
    /// accepted any <c>checklistId</c> from any authenticated
    /// user — a clear IDOR. The handler chain now goes
    /// handler → load checklist → this guard → aggregate
    /// mutation, mirroring the pattern the comment handlers
    /// adopted in the same pass.
    /// </summary>
    public static async Task<Result> EnsureCanAccessChecklistAsync(
        Guid checklistId,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Checklist? checklist = await checklists.GetByIdAsync(new ChecklistId(checklistId), ct);
        if (checklist is null)
        {
            return Result.Failure(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        return Result.Success();
    }
}
