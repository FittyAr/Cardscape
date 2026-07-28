using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>
/// The fixed set of board extensions Cardscape supports in v0.6.4.
/// New kinds are added here as we ship them; the storage layer
/// (config JSON on <see cref="BoardExtension"/>) is shape-agnostic
/// so unknown kinds won't break the data model — they just won't
/// surface in the UI until a corresponding switch case is wired up.
/// </summary>
public enum ExtensionKind
{
    /// <summary>Custom fields per card (e.g. Priority, Estimate).</summary>
    CustomFields = 0,

    /// <summary>Up/down voting on cards with vote tallies.</summary>
    Voting = 1,

    /// <summary>Repeat a card on completion (cron-like schedule).</summary>
    CardRepeater = 2
}

/// <summary>
/// A board-scoped extension toggle. The aggregate is intentionally
/// thin: it owns the (boardId, kind) tuple, a JSON config blob,
/// and an enabled flag. The interpretation of <see cref="ConfigJson"/>
/// is the responsibility of whichever feature reads it
/// (Custom Fields, Voting, Card Repeater).
/// </summary>
public sealed class BoardExtension : AggregateRoot<BoardExtensionId>
{
    public BoardId BoardId { get; private set; } = null!;
    public ExtensionKind Kind { get; private set; }
    public string? ConfigJson { get; private set; }
    public bool IsEnabled { get; private set; }

    // EF Core.
    private BoardExtension() { }

    private BoardExtension(
        BoardExtensionId id,
        BoardId boardId,
        ExtensionKind kind,
        string? configJson,
        bool isEnabled,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Kind = kind;
        ConfigJson = configJson;
        IsEnabled = isEnabled;
        CreatedAt = at;
    }

    public static Result<BoardExtension> Enable(
        BoardId boardId,
        ExtensionKind kind,
        string? configJson,
        DateTimeOffset at)
    {
        var configError = ValidateConfigJson(configJson);
        if (configError is not null)
        {
            return Result.Failure<BoardExtension>(configError);
        }

        return Result.Success(new BoardExtension(
            BoardExtensionId.New(),
            boardId,
            kind,
            string.IsNullOrWhiteSpace(configJson) ? null : configJson,
            isEnabled: true,
            at: at));
    }

    public Result UpdateConfig(string? configJson, DateTimeOffset at)
    {
        var configError = ValidateConfigJson(configJson);
        if (configError is not null)
        {
            return Result.Failure(configError);
        }

        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? null : configJson;
        StampChanged(by: null, at: at);
        return Result.Success();
    }

    public void Enable(DateTimeOffset at)
    {
        IsEnabled = true;
        StampChanged(by: null, at: at);
    }

    public Result Disable(DateTimeOffset at)
    {
        if (!IsEnabled)
        {
            return Result.Failure(DomainError.Conflict(
                "extension.already_disabled",
                "Extension is already disabled."));
        }

        IsEnabled = false;
        StampChanged(by: null, at: at);
        return Result.Success();
    }

    private static DomainError? ValidateConfigJson(string? configJson)
    {
        if (configJson is null)
        {
            return null;
        }

        if (configJson.Length > 8000)
        {
            return DomainError.Validation(
                "extension.config_too_large",
                "Extension config must be 8000 characters or fewer.");
        }

        // The shape is feature-defined, so we only guard against
        // obvious garbage: a non-empty value must at least look
        // like a JSON document (starts with { or [).
        var trimmed = configJson.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is not ('{' or '['))
        {
            return DomainError.Validation(
                "extension.config_not_json",
                "Extension config must be a JSON object or array.");
        }

        return null;
    }
}
