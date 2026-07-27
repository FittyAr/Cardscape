using Cardscape.Domain.Common;

namespace Cardscape.Domain.Cards;

/// <summary>Optional, plain-text or Markdown description of a card.</summary>
public sealed record CardDescription : IValueObject
{
    public const int MaxLength = 16_000;

    public string Value { get; }

    private CardDescription(string value) => Value = value;

    public static Result<CardDescription> Create(string? input)
    {
        if (input is null)
        {
            return Result.Success(new CardDescription(string.Empty));
        }

        var trimmed = input.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<CardDescription>(DomainError.Validation(
                "cards.description.too_long",
                $"Card description must be at most {MaxLength} characters."));
        }

        return Result.Success(new CardDescription(trimmed));
    }

    public override string ToString() => Value;
}
