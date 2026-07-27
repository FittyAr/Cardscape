using Cardscape.Domain.Common;

namespace Cardscape.Domain.Lists;

/// <summary>A non-empty list (column) name.</summary>
public sealed record ListName : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 100;

    public string Value { get; }

    private ListName(string value) => Value = value;

    public static Result<ListName> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<ListName>(DomainError.Validation(
                "lists.name.required",
                "List name is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<ListName>(DomainError.Validation(
                "lists.name.length",
                $"List name must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new ListName(trimmed));
    }

    public override string ToString() => Value;
}
