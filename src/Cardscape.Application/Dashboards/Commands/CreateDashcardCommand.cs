using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Wolverine;

namespace Cardscape.Application.Dashboards.Commands;

public sealed record CreateDashcardCommand(
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string? ConfigurationJson,
    int Position) : IMessage;

public static class CreateDashcardCommandHandler
{
    public static async Task<Result<DashcardDto>> Handle(
        CreateDashcardCommand command,
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

        var boardAccess = await MembershipGuards.EnsureCanMutateBoardAsync(
            boards, currentUser.Id.Value, command.BoardId, ct);
        if (boardAccess.IsFailure)
        {
            return Result.Failure<DashcardDto>(boardAccess.Error);
        }

        Result<Dashcard> create = Dashcard.Create(
            DashcardId.New(),
            boardAccess.Value.Id,
            command.Kind,
            command.Title,
            command.ConfigurationJson,
            command.Position,
            currentUser.Id.Value,
            clock.UtcNow);
        if (create.IsFailure)
        {
            return Result.Failure<DashcardDto>(create.Error);
        }

        await repo.AddAsync(create.Value, ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success(DashcardDto.FromEntity(create.Value));
    }
}
