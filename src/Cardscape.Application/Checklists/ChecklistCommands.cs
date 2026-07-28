using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
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

        Checklist? checklist = await checklists.GetByIdAsync(
            new ChecklistId(command.ChecklistId), ct);
        if (checklist is null)
        {
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
    public static async Task<Result<ChecklistDto>> Handle(
        AddChecklistItemCommand command,
        IChecklistRepository checklists,
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

        var textResult = ChecklistItemText.Create(command.Text);
        if (textResult.IsFailure)
        {
            return Result.Failure<ChecklistDto>(textResult.Error);
        }

        Checklist? checklist = await checklists.GetByIdAsync(
            new ChecklistId(command.ChecklistId), ct);
        if (checklist is null)
        {
            return Result.Failure<ChecklistDto>(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        Position position = Position.From(checklist.Items.Count + 1);
        checklist.AddItem(textResult.Value, position, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

public sealed record RenameChecklistItemCommand(Guid ChecklistId, Guid ItemId, string Text) : IMessage;

public static class RenameChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        RenameChecklistItemCommand command,
        IChecklistRepository checklists,
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

        var textResult = ChecklistItemText.Create(command.Text);
        if (textResult.IsFailure)
        {
            return Result.Failure<ChecklistDto>(textResult.Error);
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
        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

public sealed record ToggleChecklistItemCommand(Guid ChecklistId, Guid ItemId) : IMessage;

public static class ToggleChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        ToggleChecklistItemCommand command,
        IChecklistRepository checklists,
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
        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

public sealed record DeleteChecklistItemCommand(Guid ChecklistId, Guid ItemId) : IMessage;

public static class DeleteChecklistItemCommandHandler
{
    public static async Task<Result<ChecklistDto>> Handle(
        DeleteChecklistItemCommand command,
        IChecklistRepository checklists,
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
        return Result.Success(ChecklistDto.FromEntity(checklist));
    }
}

/// <summary>Internal helper that wraps the membership-check pattern
/// shared by all per-card operations.</summary>
internal static class ChecklistsAccess
{
    /// <summary>True if the card exists and the user is a member of
    /// the board it lives in. False if the user is anonymous,
    /// the card is missing, or the user is not a board member.
    /// Errors are reported by the calling handler with the
    /// appropriate context-specific code.</summary>
    public static async Task<bool> CanAccessCardAsync(
        Guid cardId,
        ICurrentUser currentUser,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return false;
        }

        Card? card = await cards.GetByIdAsync(new CardId(cardId), ct);
        if (card is null)
        {
            return false;
        }

        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return false;
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        return board is not null && board.IsMember(currentUser.Id.Value);
    }
}
