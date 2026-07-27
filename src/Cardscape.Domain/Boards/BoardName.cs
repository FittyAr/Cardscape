using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>A non-empty board name.</summary>
public sealed record BoardName : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 100;

    public string Value { get; }

    private BoardName(string value) => Value = value;

    public static Result<BoardName> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<BoardName>(DomainError.Validation(
                "boards.name.required",
                "Board name is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<BoardName>(DomainError.Validation(
                "boards.name.length",
                $"Board name must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new BoardName(trimmed));
    }

    public override string ToString() => Value;
}
