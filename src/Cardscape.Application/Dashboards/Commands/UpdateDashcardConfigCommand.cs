using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Wolverine;

namespace Cardscape.Application.Dashboards.Commands;

public sealed record UpdateDashcardConfigCommand(Guid DashcardId, string ConfigurationJson) : IMessage;

public static class UpdateDashcardConfigCommandHandler
{
    public static async Task<Result<DashcardDto>> Handle(
        UpdateDashcardConfigCommand command,
        IBoardRepository boards,
        IDashboardRepository repo,
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<DashcardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Dashcard? dashcard = await repo.GetByIdAsync(new DashcardId(command.DashcardId), ct);
        if (dashcard is null)
        {
            return Result.Failure<DashcardDto>(DomainError.NotFound(
                "dashcards.not_found", "Dashcard not found."));
        }

        var boardAccess = await MembershipGuards.EnsureCanMutateBoardAsync(
            boards, currentUser.Id.Value, dashcard.BoardId.Value, ct);
        if (boardAccess.IsFailure)
        {
            return Result.Failure<DashcardDto>(boardAccess.Error);
        }

        Result update = dashcard.UpdateConfiguration(command.ConfigurationJson, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<DashcardDto>(update.Error);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success(DashcardDto.FromEntity(dashcard));
    }
}
