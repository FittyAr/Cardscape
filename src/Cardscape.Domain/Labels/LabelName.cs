using Cardscape.Domain.Common;

namespace Cardscape.Domain.Labels;

/// <summary>A non-empty label name.</summary>
public sealed record LabelName : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 50;

    public string Value { get; }

    private LabelName(string value) => Value = value;

    public static Result<LabelName> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<LabelName>(DomainError.Validation(
                "labels.name.required",
                "Label name is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<LabelName>(DomainError.Validation(
                "labels.name.length",
                $"Label name must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new LabelName(trimmed));
    }

    public override string ToString() => Value;
}
