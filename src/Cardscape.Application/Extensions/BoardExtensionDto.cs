using Cardscape.Domain.Boards;

namespace Cardscape.Application.Extensions;

public sealed record BoardExtensionDto(
    Guid Id,
    Guid BoardId,
    ExtensionKind Kind,
    string? ConfigJson,
    bool IsEnabled)
{
    public static BoardExtensionDto FromEntity(BoardExtension e) => new(
        e.Id.Value,
        e.BoardId.Value,
        e.Kind,
        e.ConfigJson,
        e.IsEnabled);
}

