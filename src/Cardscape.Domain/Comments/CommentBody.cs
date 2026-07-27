using Cardscape.Domain.Common;

namespace Cardscape.Domain.Comments;

/// <summary>A non-empty comment body.</summary>
public sealed record CommentBody : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 8_000;

    public string Value { get; }

    private CommentBody(string value) => Value = value;

    public static Result<CommentBody> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<CommentBody>(DomainError.Validation(
                "comments.body.required",
                "Comment body is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<CommentBody>(DomainError.Validation(
                "comments.body.length",
                $"Comment must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new CommentBody(trimmed));
    }

    public override string ToString() => Value;
}
