using System.Text.RegularExpressions;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Members;

/// <summary>
/// A validated, lower-cased email address. Use
/// <see cref="Create(string)"/> to build an instance from a raw
/// string; the constructor performs the canonicalisation and
/// validation in one place.
/// </summary>
public sealed record EmailAddress : IValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Maximum allowed length, per the RFC 5321 practical limit.</summary>
    public const int MaxLength = 254;

    /// <summary>Canonicalised (trimmed and lower-cased) email value.</summary>
    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    /// <summary>
    /// Builds and validates an email address. Returns a
    /// <see cref="DomainError"/> with the
    /// <c>members.email.invalid</c> code on failure.
    /// </summary>
    public static Result<EmailAddress> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<EmailAddress>(DomainError.Validation(
                "members.email.required",
                "Email address is required."));
        }

        var trimmed = input.Trim().ToLowerInvariant();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<EmailAddress>(DomainError.Validation(
                "members.email.too_long",
                $"Email address must be at most {MaxLength} characters."));
        }

        if (!EmailRegex.IsMatch(trimmed))
        {
            return Result.Failure<EmailAddress>(DomainError.Validation(
                "members.email.invalid",
                "Email address is not in a valid format."));
        }

        return Result.Success(new EmailAddress(trimmed));
    }

    public override string ToString() => Value;
}
