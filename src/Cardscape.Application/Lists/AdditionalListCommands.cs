using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Lists;

/// <summary>P3.4 — Set a WIP limit on a list. The limit is the
/// hard cap (move handler rejects beyond this); the soft
/// flag means the move handler warns instead of rejecting
/// when the cap is hit (the soft limit is set one below
/// the hard cap).</summary>
public sealed record SetListLimitCommand(Guid ListId, int? Limit, bool Soft) : IMessage;

public static class SetListLimitCommandHandler
{
    public static async Task<Result> Handle(
        SetListLimitCommand command,
        IRepository<BoardList, BoardListId> lists,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(command.ListId), ct);
        if (list is null)
        {
            return Result.Failure(DomainError.NotFound(
                "lists.not_found", $"List {command.ListId} was not found."));
        }

        // Soft == true → limit becomes the soft cap, no hard cap.
        // Soft == false → limit becomes the hard cap, no soft cap.
        int? soft = command.Soft ? command.Limit : null;
        int? hard = command.Soft ? null : command.Limit;

        var setResult = list.SetLimit(soft, hard, clock.UtcNow);
        if (setResult.IsFailure)
        {
            return Result.Failure(setResult.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
