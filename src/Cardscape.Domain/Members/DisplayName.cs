using Cardscape.Domain.Common;

namespace Cardscape.Domain.Members;

/// <summary>A non-empty, trimmed display name (e.g. user-visible "John Doe").</summary>
public sealed record DisplayName : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 80;

    public string Value { get; }

    private DisplayName(string value) => Value = value;

    public static Result<DisplayName> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<DisplayName>(DomainError.Validation(
                "members.display_name.required",
                "Display name is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<DisplayName>(DomainError.Validation(
                "members.display_name.length",
                $"Display name must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new DisplayName(trimmed));
    }

    public override string ToString() => Value;
}
