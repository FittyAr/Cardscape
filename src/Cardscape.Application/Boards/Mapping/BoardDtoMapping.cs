using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;

namespace Cardscape.Application.Boards.Mapping;

/// <summary>Single projection of the <see cref="Board"/> aggregate onto <see cref="BoardDto"/>.</summary>
public static class BoardDtoMapping
{
    public static BoardDto ToDto(this Board board, bool isStarred) => new(
        board.Id.Value,
        board.WorkspaceId.Value,
        board.Name.Value,
        board.Description.Value,
        board.Visibility,
        board.IsArchived,
        isStarred,
        board.CreatedAt,
        board.Members.Count,
        board.Color?.Value);
}
