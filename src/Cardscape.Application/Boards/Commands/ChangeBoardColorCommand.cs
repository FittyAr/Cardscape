using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Mapping;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Commands;

/// <summary>
/// Sets the board background colour. <see cref="ColorName"/> is a palette
/// name (the same names the card cover API accepts); null, empty or
/// "none" clears the colour.
/// </summary>
public sealed record ChangeBoardColorCommand(Guid BoardId, string? ColorName) : IMessage;

public static class ChangeBoardColorCommandHandler
{
    public static async Task<Result<BoardDto>> HandleAsync(
        ChangeBoardColorCommand command,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        Color? color = null;
        if (!string.IsNullOrWhiteSpace(command.ColorName)
            && !string.Equals(command.ColorName, "none", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.Palette.ByName(command.ColorName);
            if (color is null)
            {
                return Result.Failure<BoardDto>(DomainError.Validation(
                    "boards.color_invalid",
                    $"Board color '{command.ColorName}' is not a known palette colour."));
            }
        }

        var changeResult = board.ChangeColor(color, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result.Failure<BoardDto>(changeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(board.ToDto(board.IsStarredBy(currentUser.Id.Value)));
    }
}
