using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Checklists;

public sealed record ChecklistItemDto(
    Guid Id,
    Guid ChecklistId,
    string Text,
    bool IsCompleted,
    int Position,
    Guid? AssignedTo);

public sealed record ChecklistDto(
    Guid Id,
    Guid CardId,
    string Title,
    IReadOnlyList<ChecklistItemDto> Items,
    int CompletedCount,
    int TotalCount)
{
    public static ChecklistDto FromEntity(Checklist c) => new(
        c.Id.Value,
        c.CardId.Value,
        c.Title.Value,
        c.Items
            .OrderBy(i => i.Position.Value)
            .Select(i => new ChecklistItemDto(
                i.Id.Value,
                i.ChecklistId.Value,
                i.Text.Value,
                i.IsCompleted,
                (int)i.Position.Value,
                i.AssignedTo))
            .ToList(),
        CompletedCount: c.Items.Count(i => i.IsCompleted),
        TotalCount: c.Items.Count);
}

public sealed record ListCardChecklistsQuery(Guid CardId) : IMessage;

public static class ListCardChecklistsQueryHandler
{
    public static async Task<Result<IReadOnlyList<ChecklistDto>>> Handle(
        ListCardChecklistsQuery query,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), ct);
        if (card is null)
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<Checklist> rows = await checklists.ListForCardAsync(
            card.Id.Value, ct);
        return Result.Success<IReadOnlyList<ChecklistDto>>(
            rows.Select(ChecklistDto.FromEntity).ToList());
    }
}

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

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return Result.Failure<ChecklistDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
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

public sealed record RenameChecklistCommand(Guid ChecklistId, string Title) : IMessage;

public static class RenameChecklistCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        RenameChecklistCommand command,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork uow,
        ICurrentUser currentUser,
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

        var rename = checklist.Rename(titleResult.Value, clock.UtcNow);
        if (rename.IsFailure)
        {
            return Result.Failure<ChecklistDto>(rename.Error);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

public sealed record DeleteChecklistCommand(Guid ChecklistId) : IMessage;

public static class DeleteChecklistCommandHandler
{
    public static async Task<Result> Handle(
        DeleteChecklistCommand command,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // v1.2.0 audit (pass 12): IDOR — see
        // ChecklistsAccess.EnsureCanAccessChecklistAsync.
        var access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return access;
        }

        Checklist? checklist = await checklists.GetByIdAsync(
            new ChecklistId(command.ChecklistId), ct);
        if (checklist is null || checklist.IsDeleted)
        {
            // BETA-2-#6 — see test-results/BETA-TEST-REPORT.md.
            //
            // RepositoryBase.GetByIdAsync uses
            // `Set.FindAsync()` which does NOT filter by
            // IsDeleted (the soft-delete column is a domain
            // concept, not an EF Core global query filter
            // here). `Checklist.Delete()` is idempotent — a
            // second call short-circuits with Success() and
            // the handler returns 204. The contract a UI /
            // MCP client expects is "second delete on an
            // already-deleted resource = 404". Treat
            // IsDeleted as NotFound for write operations;
            // the read paths still see the row because
            // their ListForCardAsync filters
            // !IsDeleted.
            return Result.Failure(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        var delete = checklist.Delete(clock.UtcNow);
        if (delete.IsFailure)
        {
            return delete;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

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
        ISearchIndex searchIndex,
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
            await searchIndex.IndexChecklistItemAsync(newItem, checklist, boardId, ct);
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
        ISearchIndex searchIndex,
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
            await searchIndex.IndexChecklistItemAsync(updated, checklist, boardId, ct);
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
