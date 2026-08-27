using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Checklists;

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

        Result<ChecklistAccessContext> access = await ChecklistsAccess.EnsureCanAccessChecklistAsync(
            command.ChecklistId, checklists, cards, lists, boards, currentUser, ct);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        Checklist checklist = access.Value.Checklist;
        if (checklist.IsDeleted)
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
