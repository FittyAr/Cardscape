using System.Text.Json;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>
/// Per-board definition of a custom field. The kind is
/// fixed at creation; renaming and reordering are fine; changing
/// the kind is not (existing values would be ambiguous).
/// </summary>
public sealed class CustomFieldDefinition : AggregateRoot<CustomFieldDefinitionId>
{
    public BoardId BoardId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public CustomFieldKind Kind { get; private set; }

    /// <summary>
    /// JSON array of dropdown option ids, only meaningful when
    /// <see cref="Kind"/> is <see cref="CustomFieldKind.Dropdown"/>.
    /// Stored as a JSON-encoded string column to keep the schema
    /// uniform across kinds.
    /// </summary>
    public string OptionsJson { get; private set; } = "[]";

    public int Position { get; private set; }

    // EF Core.
    private CustomFieldDefinition() { }

    private CustomFieldDefinition(
        CustomFieldDefinitionId id,
        BoardId boardId,
        string name,
        CustomFieldKind kind,
        string optionsJson,
        int position,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Name = name;
        Kind = kind;
        OptionsJson = optionsJson;
        Position = position;
        CreatedAt = at;
    }

    public static Result<CustomFieldDefinition> Create(
        BoardId boardId,
        string name,
        CustomFieldKind kind,
        IReadOnlyList<string>? dropdownOptions,
        int position,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<CustomFieldDefinition>(DomainError.Validation(
                "custom_fields.name_required", "Field name is required."));
        }

        string trimmed = name.Trim();
        if (trimmed.Length > 120)
        {
            return Result.Failure<CustomFieldDefinition>(DomainError.Validation(
                "custom_fields.name_too_long", "Field name must be 120 characters or fewer."));
        }

        string optionsJson;
        if (kind == CustomFieldKind.Dropdown)
        {
            if (dropdownOptions is null || dropdownOptions.Count < 2)
            {
                return Result.Failure<CustomFieldDefinition>(DomainError.Validation(
                    "custom_fields.dropdown_options_required",
                    "Dropdown fields require at least two options."));
            }

            if (dropdownOptions.Any(string.IsNullOrWhiteSpace))
            {
                return Result.Failure<CustomFieldDefinition>(DomainError.Validation(
                    "custom_fields.dropdown_option_blank",
                    "Dropdown options must not be blank."));
            }

            if (dropdownOptions.Select(o => o.Trim().ToLowerInvariant()).Distinct().Count() != dropdownOptions.Count)
            {
                return Result.Failure<CustomFieldDefinition>(DomainError.Validation(
                    "custom_fields.dropdown_options_duplicate",
                    "Dropdown options must be unique."));
            }

            string[] sanitized = dropdownOptions.Select(o => o.Trim()).ToArray();
            optionsJson = JsonSerializer.Serialize(sanitized);
        }
        else
        {
            optionsJson = "[]";
        }

        return Result.Success(new CustomFieldDefinition(
            CustomFieldDefinitionId.New(),
            boardId,
            trimmed,
            kind,
            optionsJson,
            position,
            at));
    }

    public Result Rename(string newName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.name_required", "Field name is required."));
        }

        string trimmed = newName.Trim();
        if (trimmed.Length > 120)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.name_too_long", "Field name must be 120 characters or fewer."));
        }

        Name = trimmed;
        StampChanged(by: null, at: at);
        return Result.Success();
    }

    public void Reorder(int newPosition, DateTimeOffset at)
    {
        Position = newPosition;
        StampChanged(by: null, at: at);
    }
}
