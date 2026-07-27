using Cardscape.Domain.Common;

namespace Cardscape.Domain.Checklists;

/// <summary>A non-empty checklist title.</summary>
public sealed record ChecklistTitle : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 200;

    public string Value { get; }

    private ChecklistTitle(string value) => Value = value;

    public static Result<ChecklistTitle> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<ChecklistTitle>(DomainError.Validation(
                "checklists.title.required",
                "Checklist title is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<ChecklistTitle>(DomainError.Validation(
                "checklists.title.length",
                $"Checklist title must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new ChecklistTitle(trimmed));
    }

    public override string ToString() => Value;
}
