using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Extensions;

public sealed record UpdateBoardExtensionConfigCommand(
    Guid BoardId,
    int Kind,
    string? ConfigJson) : IMessage;

public static class UpdateBoardExtensionConfigCommandHandler
{
    public static async Task<Result<BoardExtensionDto>> Handle(
        UpdateBoardExtensionConfigCommand command,
        IBoardExtensionRepository extensions,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardExtensionDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (!Enum.IsDefined(typeof(ExtensionKind), command.Kind))
        {
            return Result.Failure<BoardExtensionDto>(DomainError.Validation(
                "extension.unknown_kind",
                $"Unknown extension kind: {command.Kind}."));
        }

        var kind = (ExtensionKind)command.Kind;
        var board = await boards.GetWithMembersAsync(
            new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardExtensionDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardExtensionDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var existing = await extensions.GetByBoardAndKindAsync(board.Id, kind, cancellationToken);
        if (existing is null)
        {
            return Result.Failure<BoardExtensionDto>(DomainError.NotFound(
                "extension.not_found",
                "Extension is not enabled for this board."));
        }

        var update = existing.UpdateConfig(command.ConfigJson, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<BoardExtensionDto>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(BoardExtensionDto.FromEntity(existing));
    }
}


