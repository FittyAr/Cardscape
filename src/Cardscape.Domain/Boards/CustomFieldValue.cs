using System.Globalization;
using System.Text.Json;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>
/// Per-card, per-field value. A <see cref="Card"/> can have at most
/// one <see cref="CustomFieldValue"/> per <see cref="CustomFieldDefinition"/>;
/// setting the value creates or updates the row.
/// </summary>
public sealed class CustomFieldValue : AggregateRoot<CustomFieldValueId>
{
    public CustomFieldDefinitionId FieldDefinitionId { get; private set; } = null!;
    public CardId CardId { get; private set; } = null!;

    /// <summary>
    /// The serialized value. Shape depends on the field's
    /// <see cref="CustomFieldDefinition.Kind"/>:
    /// <list type="bullet">
    ///   <item>Text — JSON string</item>
    ///   <item>Number — JSON number</item>
    ///   <item>Date — ISO-8601 string</item>
    ///   <item>Dropdown — JSON string (one of the option ids)</item>
    ///   <item>Checkbox — JSON boolean</item>
    /// </list>
    /// Empty string means "no value".
    /// </summary>
    public string ValueJson { get; private set; } = string.Empty;

    // EF Core.
    private CustomFieldValue() { }

    private CustomFieldValue(
        CustomFieldValueId id,
        CustomFieldDefinitionId fieldDefinitionId,
        CardId cardId,
        string valueJson,
        DateTimeOffset at)
    {
        Id = id;
        FieldDefinitionId = fieldDefinitionId;
        CardId = cardId;
        ValueJson = valueJson;
        CreatedAt = at;
    }

    public static Result<CustomFieldValue> Create(
        CustomFieldDefinitionId fieldDefinitionId,
        CardId cardId,
        string valueJson,
        DateTimeOffset at) =>
        Result.Success(new CustomFieldValue(
            CustomFieldValueId.New(),
            fieldDefinitionId,
            cardId,
            string.IsNullOrEmpty(valueJson) ? string.Empty : valueJson,
            at));

    /// <summary>
    /// Replaces the stored value. Returns a failure if the JSON
    /// shape doesn't match the field's <paramref name="kind"/>.
    /// </summary>
    public Result SetValue(string newValueJson, CustomFieldKind kind, DateTimeOffset at)
    {
        if (string.IsNullOrEmpty(newValueJson))
        {
            ValueJson = string.Empty;
            StampChanged(by: null, at: at);
            return Result.Success();
        }

        Result validation = ValidateShape(newValueJson, kind);
        if (validation.IsFailure)
        {
            return validation;
        }

        ValueJson = newValueJson;
        StampChanged(by: null, at: at);
        return Result.Success();
    }

    public static Result ValidateShape(string valueJson, CustomFieldKind kind) =>
        kind switch
        {
            CustomFieldKind.Text => ValidateText(valueJson),
            CustomFieldKind.Number => ValidateNumber(valueJson),
            CustomFieldKind.Date => ValidateDate(valueJson),
            CustomFieldKind.Dropdown => ValidateDropdown(valueJson),
            CustomFieldKind.Checkbox => ValidateCheckbox(valueJson),
            _ => Result.Failure(DomainError.Validation(
                "custom_fields.kind_unknown", $"Unknown custom field kind: {kind}."))
        };

    private static Result ValidateText(string valueJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(valueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.String)
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.value_not_text", "Text value must be a JSON string."));
            }
            string text = doc.RootElement.GetString()!;
            if (text.Length > 4000)
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.text_too_long", "Text value must be 4000 characters or fewer."));
            }
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.value_not_text", $"Text value must be a JSON string: {ex.Message}"));
        }
    }

    private static Result ValidateNumber(string valueJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(valueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Number)
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.value_not_number", "Number value must be a JSON number."));
            }
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.value_not_number", $"Number value must be a JSON number: {ex.Message}"));
        }
    }

    private static Result ValidateDate(string valueJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(valueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.String)
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.value_not_date", "Date value must be a JSON string."));
            }

            string text = doc.RootElement.GetString()!;
            if (!DateTimeOffset.TryParse(
                text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.value_not_iso8601", "Date value must be ISO-8601."));
            }
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.value_not_date", $"Date value must be a JSON string: {ex.Message}"));
        }
    }

    private static Result ValidateDropdown(string valueJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(valueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.String)
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.value_not_dropdown", "Dropdown value must be a JSON string."));
            }
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.value_not_dropdown", $"Dropdown value must be a JSON string: {ex.Message}"));
        }
    }

    private static Result ValidateCheckbox(string valueJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(valueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.True && doc.RootElement.ValueKind != JsonValueKind.False)
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.value_not_bool", "Checkbox value must be a JSON boolean."));
            }
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.value_not_bool", $"Checkbox value must be a JSON boolean: {ex.Message}"));
        }
    }
}
