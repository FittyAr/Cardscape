namespace Cardscape.Application.Lists.DTOs;

public sealed record BoardListDto(
    Guid Id,
    Guid BoardId,
    string Name,
    double Position,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int CardCount);
