using Cardscape.Domain.Common;

namespace Cardscape.Domain.Authentication.ExternalLogins;

/// <summary>
/// The provider-assigned unique id of an authenticated user
/// (the <c>sub</c> claim for OIDC, the <c>id</c> field for
/// Google, the <c>oid</c> for Microsoft). Combined with
/// <see cref="ExternalProvider"/> this is the natural key
/// that links a Cardscape user to their external identity.
/// </summary>
public sealed record SubjectId : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 256;

    public string Value { get; }

    private SubjectId(string value) => Value = value;

    public static Result<SubjectId> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<SubjectId>(DomainError.Validation(
                "auth.external.subject_required",
                "External subject id is required."));
        }

        var trimmed = input.Trim();
        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<SubjectId>(DomainError.Validation(
                "auth.external.subject_length",
                $"External subject id must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new SubjectId(trimmed));
    }

    public override string ToString() => Value;
}
