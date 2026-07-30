using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class SamlConnectionRepository(CardscapeDbContext db) : ISamlConnectionRepository
{
    public Task<SamlConnection?> FindByIdAsync(SamlConnectionId id, CancellationToken ct = default) =>
        db.SamlConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<SamlConnection?> FindBySlugAsync(string slug, CancellationToken ct = default) =>
        db.SamlConnections.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public Task<SamlConnection?> FindByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default) =>
        db.SamlConnections
            .FirstOrDefaultAsync(c => c.WorkspaceId == new Domain.Workspaces.WorkspaceId(workspaceId), ct);

    public async Task AddAsync(SamlConnection connection, CancellationToken ct = default)
    {
        await db.SamlConnections.AddAsync(connection, ct);
    }
}
