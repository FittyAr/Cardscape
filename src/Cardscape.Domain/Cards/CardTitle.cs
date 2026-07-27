using Cardscape.Domain.Common;

namespace Cardscape.Domain.Cards;

/// <summary>A non-empty card title.</summary>
public sealed record CardTitle : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 500;

    public string Value { get; }

    private CardTitle(string value) => Value = value;

    public static Result<CardTitle> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<CardTitle>(DomainError.Validation(
                "cards.title.required",
                "Card title is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<CardTitle>(DomainError.Validation(
                "cards.title.length",
                $"Card title must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new CardTitle(trimmed));
    }

    public override string ToString() => Value;
}
