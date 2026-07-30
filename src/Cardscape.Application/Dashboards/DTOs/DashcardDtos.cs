using Cardscape.Domain.Dashboards;

namespace Cardscape.Application.Dashboards.DTOs;

public sealed record DashcardDto(
    Guid Id,
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string? ConfigurationJson,
    int Position,
    DateTimeOffset CreatedAt);

public sealed record CreateDashcardRequest(Guid BoardId, DashcardKind Kind, string Title, string? ConfigurationJson, int Position);
public sealed record UpdateDashcardConfigRequest(string ConfigurationJson);
