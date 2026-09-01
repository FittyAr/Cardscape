using System.Text.Json;
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
    public const int MaxConfigurationLength = 8192;

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

        if (!Enum.IsDefined(kind))
        {
            return Result.Failure<Dashcard>(DomainError.Validation(
                "dashboards.kind_invalid",
                $"Dashcard kind value '{(int)kind}' is not a defined DashcardKind member."));
        }

        Result configuration = ValidateConfiguration(configurationJson);
        if (configuration.IsFailure)
        {
            return Result.Failure<Dashcard>(configuration.Error);
        }

        return Result.Success(new Dashcard(
            id, boardId, kind, title.Trim(), configurationJson, position, createdBy, at));
    }

    public Result UpdateConfiguration(string? configurationJson, DateTimeOffset at)
    {
        Result validation = ValidateConfiguration(configurationJson);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (ConfigurationJson == configurationJson)
        {
            return Result.Success();
        }

        ConfigurationJson = configurationJson;
        UpdatedAt = at;
        return Result.Success();
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

    private static Result ValidateConfiguration(string? configurationJson)
    {
        if (configurationJson?.Length > MaxConfigurationLength)
        {
            return Result.Failure(DomainError.Validation(
                "dashcards.config_too_large",
                $"Dashcard config is too large (max {MaxConfigurationLength / 1024} KB)."));
        }

        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return Result.Success();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(configurationJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Result.Success()
                : Result.Failure(DomainError.Validation(
                    "dashboards.configuration_not_object",
                    "ConfigurationJson must be a JSON object (e.g. {} or {\"key\":\"value\"})."));
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "dashboards.configuration_invalid_json",
                $"ConfigurationJson is not valid JSON: {ex.Message}"));
        }
    }
}
