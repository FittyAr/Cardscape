using Cardscape.Domain.Common;

namespace Cardscape.Domain.Checklists;

/// <summary>Text content of a checklist item (may be empty to model
/// a placeholder item).</summary>
public sealed record ChecklistItemText : IValueObject
{
    public const int MaxLength = 500;

    public string Value { get; }

    private ChecklistItemText(string value) => Value = value;

    public static Result<ChecklistItemText> Create(string? input)
    {
        var trimmed = (input ?? string.Empty).Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<ChecklistItemText>(DomainError.Validation(
                "checklists.item_text.too_long",
                $"Checklist item text must be at most {MaxLength} characters."));
        }

        return Result.Success(new ChecklistItemText(trimmed));
    }

    public override string ToString() => Value;
}
