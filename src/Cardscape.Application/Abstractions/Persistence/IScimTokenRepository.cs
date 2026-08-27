using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IScimTokenRepository
{
    Task<ScimToken?> FindByIdAsync(ScimTokenId id, CancellationToken ct = default);
    Task<ScimToken?> FindByPlaintextAsync(string plaintext, CancellationToken ct = default);
    Task<IReadOnlyList<ScimToken>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default);
    Task AddAsync(ScimToken token, CancellationToken ct = default);
}
