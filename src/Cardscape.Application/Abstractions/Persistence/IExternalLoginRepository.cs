using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="ExternalLogin"/>.
/// The (Provider, Subject) pair is unique; the
/// <see cref="FindByProviderSubjectAsync"/> lookup is the
/// hot path of the external-login callback.
/// </summary>
public interface IExternalLoginRepository : IRepository<ExternalLogin, ExternalLoginId>
{
    /// <summary>
    /// Looks up a link by (provider, subject). Returns
    /// <c>null</c> if the external identity has not been
    /// linked to a Cardscape user yet.
    /// </summary>
    Task<ExternalLogin?> FindByProviderSubjectAsync(
        ExternalProvider provider,
        SubjectId subject,
        CancellationToken ct = default);

    /// <summary>
    /// Lists every external identity the given user has
    /// connected. Used by the Web UI "Connected accounts"
    /// section.
    /// </summary>
    Task<IReadOnlyList<ExternalLogin>> ListForUserAsync(
        UserId userId,
        CancellationToken ct = default);
}
