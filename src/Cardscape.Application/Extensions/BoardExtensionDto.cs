using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Extensions;

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


