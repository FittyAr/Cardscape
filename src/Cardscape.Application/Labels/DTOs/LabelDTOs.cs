namespace Cardscape.Application.Labels.DTOs;

public sealed record LabelDto(
    Guid Id,
    Guid BoardId,
    string Name,
    string Color);
