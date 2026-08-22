using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Authentication;

/// <summary>
/// Stores short-lived, single-use tokens between password verification and
/// completion of the second TOTP authentication step.
/// </summary>
public interface IPendingTotpLoginStore
{
    /// <summary>Mints an opaque, short-lived token bound to a user.</summary>
    string Mint(UserId userId);

    /// <summary>
    /// Atomically consumes a token and returns its user, or <see langword="null"/>
    /// when the token is unknown, expired, or already consumed.
    /// </summary>
    UserId? Consume(string token);
}
