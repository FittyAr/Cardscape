using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Extensions;

public sealed record EnableBoardExtensionCommand(
    Guid BoardId,
    int Kind,
    string? ConfigJson) : IMessage;

public static class EnableBoardExtensionCommandHandler
{
    public static async Task<Result<BoardExtensionDto>> Handle(
        EnableBoardExtensionCommand command,
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
        if (existing is not null)
        {
            if (!existing.IsEnabled)
            {
                existing.Enable(clock.UtcNow);
            }

            if (command.ConfigJson is not null)
            {
                var update = existing.UpdateConfig(command.ConfigJson, clock.UtcNow);
                if (update.IsFailure)
                {
                    return Result.Failure<BoardExtensionDto>(update.Error);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(BoardExtensionDto.FromEntity(existing));
        }

        var creation = BoardExtension.Enable(board.Id, kind, command.ConfigJson, clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<BoardExtensionDto>(creation.Error);
        }

        await extensions.AddAsync(creation.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BoardExtensionDto.FromEntity(creation.Value));
    }
}

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

        if (!Enum.IsDefined(typeof(ExtensionKind), command.Kind))
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

public sealed record ListBoardExtensionsQuery(Guid BoardId) : IMessage;

public static class ListBoardExtensionsQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardExtensionDto>>> Handle(
        ListBoardExtensionsQuery query,
        IBoardExtensionRepository extensions,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardExtensionDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<BoardExtensionDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<BoardExtensionDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var rows = await extensions.ListForBoardAsync(board.Id, cancellationToken);
        return Result.Success<IReadOnlyList<BoardExtensionDto>>(
            rows.Select(BoardExtensionDto.FromEntity).ToList());
    }
}

public sealed record BoardExtensionDto(
    Guid Id,
    Guid BoardId,
    int Kind,
    string? ConfigJson,
    bool IsEnabled)
{
    public static BoardExtensionDto FromEntity(BoardExtension e) => new(
        e.Id.Value,
        e.BoardId.Value,
        (int)e.Kind,
        e.ConfigJson,
        e.IsEnabled);
}
