using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Checklists;

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
