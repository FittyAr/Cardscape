using Cardscape.Domain.Labels;

namespace Cardscape.Application.Labels.DTOs;

public sealed record LabelDto(
    Guid Id,
    Guid BoardId,
    string Name,
    string Color)
{
    public static LabelDto FromEntity(Label label) => new(
        label.Id.Value,
        label.BoardId.Value,
        label.Name.Value,
        label.Color.Value);
}
