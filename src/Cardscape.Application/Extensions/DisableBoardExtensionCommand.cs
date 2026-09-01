using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Extensions;

public sealed record DisableBoardExtensionCommand(
    Guid BoardId,
    int Kind) : IMessage;

public static class DisableBoardExtensionCommandHandler
{
    public static async Task<Result> Handle(
        DisableBoardExtensionCommand command,
        IBoardExtensionRepository extensions,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (!Enum.IsDefined((ExtensionKind)command.Kind))
        {
            return Result.Failure(DomainError.Validation(
                "extension.unknown_kind",
                $"Unknown extension kind: {command.Kind}."));
        }

        var kind = (ExtensionKind)command.Kind;
        var board = await boards.GetWithMembersAsync(
            new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var existing = await extensions.GetByBoardAndKindAsync(board.Id, kind, cancellationToken);
        if (existing is null)
        {
            return Result.Failure(DomainError.NotFound(
                "extension.not_found",
                "Extension is not enabled for this board."));
        }

        var result = existing.Disable(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
