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
