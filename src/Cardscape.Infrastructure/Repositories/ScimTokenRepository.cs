using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class ScimTokenRepository(CardscapeDbContext db) : IScimTokenRepository
{
    public Task<ScimToken?> FindByIdAsync(ScimTokenId id, CancellationToken ct = default) =>
        db.ScimTokens.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<ScimToken?> FindByPlaintextAsync(string plaintext, CancellationToken ct = default)
    {
        // The token is hashed at the domain layer, so we look
        // it up by hashing the presented plaintext against
        // every non-revoked row. For a v1.1.0 the SCIM token
        // list per workspace is small (typically 1) so the
        // linear scan is fine; a follow-up PR can introduce
        // a faster lookup keyed on a HMAC prefix.
        await foreach (var token in db.ScimTokens.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!token.IsRevoked && token.Verify(plaintext))
            {
                return token;
            }
        }
        return null;
    }

    public async Task<IReadOnlyList<ScimToken>> ListForWorkspaceAsync(Guid workspaceId, CancellationToken ct = default) =>
        await db.ScimTokens
            .Where(t => t.WorkspaceId == new Domain.Workspaces.WorkspaceId(workspaceId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ScimToken token, CancellationToken ct = default)
    {
        await db.ScimTokens.AddAsync(token, ct);
    }
}
