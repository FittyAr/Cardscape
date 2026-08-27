using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Wolverine;

namespace Cardscape.Application.Dashboards.Commands;

public sealed record DeleteDashcardCommand(Guid DashcardId) : IMessage;

public static class DeleteDashcardCommandHandler
{
    public static async Task<Result> Handle(
        DeleteDashcardCommand command,
        IBoardRepository boards,
        IDashboardRepository repo,
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

        Dashcard? dashcard = await repo.GetByIdAsync(new DashcardId(command.DashcardId), ct);
        if (dashcard is null)
        {
            return Result.Failure(DomainError.NotFound(
                "dashcards.not_found", "Dashcard not found."));
        }

        var boardAccess = await MembershipGuards.EnsureCanMutateBoardAsync(
            boards, currentUser.Id.Value, dashcard.BoardId.Value, ct);
        if (boardAccess.IsFailure)
        {
            return Result.Failure(boardAccess.Error);
        }

        dashcard.Delete(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
