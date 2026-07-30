using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Dashboards;

/// <summary>
/// A dashcard is a board-level widget that aggregates card
/// metrics. The kind determines the aggregation (overdue
/// count, by member, by label, by list, due this week).
/// The config is an opaque JSON blob that the specific
/// widget reads at render time.
/// </summary>
public sealed class Dashcard : AggregateRoot<DashcardId>
{
    public BoardId BoardId { get; private set; } = null!;
    public DashcardKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? ConfigurationJson { get; private set; }
    public int Position { get; private set; }

    private Dashcard() { }

    private Dashcard(
        DashcardId id,
        BoardId boardId,
        DashcardKind kind,
        string title,
        string? configurationJson,
        int position,
        Guid createdBy,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Kind = kind;
        Title = title;
        ConfigurationJson = configurationJson;
        Position = position;
        CreatedBy = createdBy;
        CreatedAt = at;
    }

    public static Result<Dashcard> Create(
        DashcardId id,
        BoardId boardId,
        DashcardKind kind,
        string title,
        string? configurationJson,
        int position,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Dashcard>(DomainError.Validation(
                "dashboards.title_required", "Dashcard title is required."));
        }

        return Result.Success(new Dashcard(
            id, boardId, kind, title.Trim(), configurationJson, position, createdBy, at));
    }

    public void Delete(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        UpdatedAt = at;
    }
}
