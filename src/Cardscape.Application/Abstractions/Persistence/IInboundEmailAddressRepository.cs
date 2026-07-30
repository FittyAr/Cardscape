using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="InboundEmailAddress"/>.</summary>
public interface IInboundEmailAddressRepository : IRepository<InboundEmailAddress, InboundEmailAddressId>
{
    /// <summary>Lists every active (and inactive) inbound email
    /// address for a workspace. The settings UI shows disabled
    /// addresses so the user can re-enable them.</summary>
    Task<IReadOnlyList<InboundEmailAddress>> ListForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default);

    /// <summary>Finds the inbound address record that owns the
    /// given (case-insensitive) email. Returns <c>null</c> when
    /// the address is not registered (e.g. a spam relay hitting
    /// the public webhook URL).</summary>
    Task<InboundEmailAddress?> FindByEmailAsync(
        string email, CancellationToken ct = default);
}
