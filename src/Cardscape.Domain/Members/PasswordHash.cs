using Cardscape.Domain.Common;

namespace Cardscape.Domain.Members;

/// <summary>
/// A self-contained password hash produced by an
/// <c>IPasswordHasher</c> implementation. The value object does
/// not know the algorithm — the format encodes it as a single
/// string (e.g. <c>v1.salt.hash</c>).
/// </summary>
/// <remarks>
/// We keep the hash as an opaque, algorithm-agnostic string. When
/// the hashing algorithm changes (today PBKDF2, tomorrow Argon2id)
/// the migration is just "if the prefix is <c>v1</c>, re-hash on
/// next successful login".
/// </remarks>
public sealed record PasswordHash : IValueObject
{
    /// <summary>Algorithm/version prefix used in stored hashes.</summary>
    public const string VersionPrefix = "v1";

    /// <summary>Raw, opaque, algorithm-tagged hash string.</summary>
    public string Value { get; }

    private PasswordHash(string value) => Value = value;

    /// <summary>
    /// Wraps a hash string produced by the password hasher.
    /// Validation is the hasher's responsibility; the value object
    /// just guarantees non-empty.
    /// </summary>
    public static Result<PasswordHash> FromHashed(string hashed)
    {
        if (string.IsNullOrWhiteSpace(hashed))
        {
            return Result.Failure<PasswordHash>(DomainError.Validation(
                "members.password.empty",
                "Password hash cannot be empty."));
        }

        return Result.Success(new PasswordHash(hashed));
    }

    public override string ToString() => Value;
}
