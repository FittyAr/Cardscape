using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>Optional, plain-text description of a board.</summary>
public sealed record BoardDescription : IValueObject
{
    public const int MaxLength = 2_000;

    public string Value { get; }

    private BoardDescription(string value) => Value = value;

    public static Result<BoardDescription> Create(string? input)
    {
        if (input is null)
        {
            return Result.Success(new BoardDescription(string.Empty));
        }

        var trimmed = input.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<BoardDescription>(DomainError.Validation(
                "boards.description.too_long",
                $"Board description must be at most {MaxLength} characters."));
        }

        return Result.Success(new BoardDescription(trimmed));
    }

    public override string ToString() => Value;
}
