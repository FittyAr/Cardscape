using Cardscape.Domain.Lists;

namespace Cardscape.Application.Lists.DTOs;

public sealed record BoardListDto(
    Guid Id,
    Guid BoardId,
    string Name,
    double Position,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int CardCount)
{
    public static BoardListDto FromEntity(BoardList list, int cardCount = 0) => new(
        list.Id.Value,
        list.BoardId.Value,
        list.Name.Value,
        list.Position.Value,
        list.IsArchived,
        list.CreatedAt,
        cardCount);
}
