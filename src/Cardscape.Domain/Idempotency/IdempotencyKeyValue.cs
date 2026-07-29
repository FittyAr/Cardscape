using Cardscape.Domain.Common;

namespace Cardscape.Domain.Idempotency;

/// <summary>
/// A user-supplied, opaque, deterministic key for an idempotent
/// request. A client that wants "exactly-once" semantics
/// generates this value (typically a UUID) and sends it on
/// every retry of the same logical request. The middleware
/// uses the key to short-circuit duplicates to the stored
/// response.
/// </summary>
public sealed record IdempotencyKeyValue : IValueObject
{
    public const int MinLength = 8;
    public const int MaxLength = 200;

    public string Value { get; }

    private IdempotencyKeyValue(string value) => Value = value;

    public static Result<IdempotencyKeyValue> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<IdempotencyKeyValue>(DomainError.Validation(
                "idempotency.key.required",
                "Idempotency key is required."));
        }

        var trimmed = input.Trim();
        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<IdempotencyKeyValue>(DomainError.Validation(
                "idempotency.key.length",
                $"Idempotency key must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new IdempotencyKeyValue(trimmed));
    }

    public override string ToString() => Value;
}
