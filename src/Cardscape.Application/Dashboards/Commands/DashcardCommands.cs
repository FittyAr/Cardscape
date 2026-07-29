using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Wolverine;

namespace Cardscape.Application.Dashboards.Commands;

public sealed record CreateDashcardCommand(
    Guid BoardId,
    int Kind,
    string Title,
    string? ConfigurationJson) : IMessage;

public static class CreateDashcardCommandHandler
{
    public static async Task<Result<DashcardDto>> Handle(
        CreateDashcardCommand command,
        IBoardRepository boards,
        IDashboardRepository dashboards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<DashcardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (!Enum.IsDefined(typeof(DashcardKind), command.Kind))
        {
            return Result.Failure<DashcardDto>(DomainError.Validation(
                "dashboards.unknown_kind",
                $"Unknown dashcard kind: {command.Kind}."));
        }

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<DashcardDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<DashcardDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var creation = Dashcard.Create(
            DashcardId.New(),
            board.Id,
            (DashcardKind)command.Kind,
            command.Title,
            command.ConfigurationJson,
            position: 0,
            createdBy: currentUser.Id.Value,
            at: clock.UtcNow);

        if (creation.IsFailure)
        {
            return Result.Failure<DashcardDto>(creation.Error);
        }

        await dashboards.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new DashcardDto(
            creation.Value.Id.Value,
            creation.Value.BoardId.Value,
            creation.Value.Kind,
            creation.Value.Title,
            creation.Value.ConfigurationJson,
            creation.Value.Position));
    }
}

public sealed record DeleteDashcardCommand(Guid DashcardId) : IMessage;

public static class DeleteDashcardCommandHandler
{
    public static async Task<Result> Handle(
        DeleteDashcardCommand command,
        IBoardRepository boards,
        IDashboardRepository dashboards,
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

        var dashcard = await dashboards.GetByIdAsync(new DashcardId(command.DashcardId), ct);
        if (dashcard is null)
        {
            return Result.Failure(DomainError.NotFound(
                "dashboards.not_found", "Dashcard was not found."));
        }

        var board = await boards.GetByIdAsync(dashcard.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        dashcard.Delete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
