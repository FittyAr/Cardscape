using Cardscape.Domain.Common;

namespace Cardscape.Domain.Security;

/// <summary>A non-empty, human-readable label for an API token.</summary>
public sealed record ApiTokenName : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 80;

    public string Value { get; }

    private ApiTokenName(string value) => Value = value;

    public static Result<ApiTokenName> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<ApiTokenName>(DomainError.Validation(
                "security.api_token.name_required",
                "API token name is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<ApiTokenName>(DomainError.Validation(
                "security.api_token.name_length",
                $"API token name must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new ApiTokenName(trimmed));
    }

    public override string ToString() => Value;
}
