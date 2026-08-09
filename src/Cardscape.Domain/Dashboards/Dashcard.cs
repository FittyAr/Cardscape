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

        // BETA-A3-R2-006 — see
        // test-results/beta/round-2/reports/A3-boards.md.
        // The previous version of Create accepted any integer
        // for `kind` (the JSON deserialiser casts into the
        // enum, including undefined members). A kind value
        // outside the known set slipped into the DB and the
        // UI rendered nothing for that card. The fix is the
        // same `Enum.IsDefined` check used for Region
        // (BUG-A2-010): reject at the domain edge so the
        // corrupted value never reaches the change tracker.
        if (!Enum.IsDefined(typeof(DashcardKind), kind))
        {
            return Result.Failure<Dashcard>(DomainError.Validation(
                "dashboards.kind_invalid",
                $"Dashcard kind value '{(int)kind}' is not a defined DashcardKind member."));
        }

        // BETA-A3-R2-007 — see
        // test-results/beta/round-2/reports/A3-boards.md.
        // ConfigurationJson was stored verbatim. The Blazor
        // UI parses it with System.Text.Json, so a malformed
        // payload was caught at render time and produced a
        // broken card. Validate the JSON shape at create
        // time: a non-empty value must be parseable, and the
        // top-level must be a JSON object (the card UI binds
        // to keys at the root, not to a top-level array /
        // scalar). Empty / null stays allowed (some dashcards
        // have no config at all).
        if (!string.IsNullOrWhiteSpace(configurationJson))
        {
            try
            {
                using System.Text.Json.JsonDocument doc =
                    System.Text.Json.JsonDocument.Parse(configurationJson);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    return Result.Failure<Dashcard>(DomainError.Validation(
                        "dashboards.configuration_not_object",
                        "ConfigurationJson must be a JSON object (e.g. {} or {\"key\":\"value\"})."));
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                return Result.Failure<Dashcard>(DomainError.Validation(
                    "dashboards.configuration_invalid_json",
                    $"ConfigurationJson is not valid JSON: {ex.Message}"));
            }
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
