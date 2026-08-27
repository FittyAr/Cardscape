using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="TotpCredential"/>.
/// The <see cref="FindForUserAsync"/> lookup is the hot
/// path of every sign-in and sensitive action.
/// </summary>
public interface ITotpCredentialRepository : IRepository<TotpCredential, TotpCredentialId>
{
    /// <summary>Returns the (at most one) TOTP credential
    /// the given user has enrolled. <c>null</c> when the
    /// user has not enrolled 2FA.</summary>
    Task<TotpCredential?> FindForUserAsync(UserId userId, CancellationToken ct = default);

    Task<bool> AreActiveForAllUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken ct = default);
}
