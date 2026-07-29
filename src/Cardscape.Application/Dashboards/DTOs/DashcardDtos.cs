using Cardscape.Domain.Dashboards;

namespace Cardscape.Application.Dashboards.DTOs;

public sealed record DashcardDto(
    Guid Id,
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string? ConfigurationJson,
    int Position);
