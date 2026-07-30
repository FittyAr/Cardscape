using Cardscape.Domain.Authentication.Saml;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ISamlConnectionRepository
{
    Task<SamlConnection?> FindByIdAsync(SamlConnectionId id, CancellationToken ct = default);
    Task<SamlConnection?> FindBySlugAsync(string slug, CancellationToken ct = default);
    Task<SamlConnection?> FindByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task AddAsync(SamlConnection connection, CancellationToken ct = default);
}
