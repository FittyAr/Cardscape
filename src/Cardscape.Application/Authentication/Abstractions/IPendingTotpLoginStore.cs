using Cardscape.Domain.Members;

namespace Cardscape.Application.Authentication.Abstractions;

/// <summary>
/// Backs the two-step TOTP login flow:
/// <list type="number">
///   <item><c>LoginUserQuery</c> confirms the email+password and
///         mints a one-shot <c>PendingTotpToken</c> via
///         <see cref="Mint"/>.</item>
///   <item>The Web UI posts the token + a 6-digit TOTP code to
///         <c>POST /api/auth/login/totp</c>; the handler calls
///         <see cref="Consume"/> to atomically resolve the token
///         back to a <see cref="UserId"/> and erase it.</item>
/// </list>
/// Implementations must enforce a short TTL (5 min is plenty for
/// a user to type the code) and make <see cref="Consume"/>
/// single-use â€” a token is consumed the first time it is
/// successfully read, regardless of whether the verification that
/// follows succeeds.
/// </summary>
public interface IPendingTotpLoginStore
{
    /// <summary>
    /// Mints a fresh opaque token bound to <paramref name="userId"/>.
    /// The token is what the Web UI returns to the browser; the
    /// browser must present it again (with a TOTP code) at
    /// <c>/api/auth/login/totp</c>.
    /// </summary>
    string Mint(UserId userId);

    /// <summary>
    /// Atomically reads the user id behind <paramref name="token"/>
    /// and removes the entry. Returns <c>null</c> for an unknown,
    /// expired, or already-consumed token.
    /// </summary>
    UserId? Consume(string token);
}
