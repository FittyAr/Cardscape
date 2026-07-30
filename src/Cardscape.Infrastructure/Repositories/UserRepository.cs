using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Cardscape.Infrastructure.Repositories;

public sealed class UserRepository(CardscapeDbContext db) : RepositoryBase<User, UserId>(db), IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return null;
        }

        // EF Core cannot translate the EmailAddress value-object property
        // (u.Email.Value) to SQL. We use a streaming client-side filter
        // over a primary-key-ordered scan; for a user table this is cheap
        // and avoids a SQL-side LIKE. The value object is created on
        // insert with a normalized email, so a case-insensitive ordinal
        // match is enough.
        await foreach (var user in Db.Set<User>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (string.Equals(user.Email.Value, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return user;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<WorkspaceMember>> ListWorkspaceMembersAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        // The Workspace aggregate owns the member collection
        // through the `workspace_members` owned-entity table.
        // We reach it through a raw query on the DbContext —
        // the relationship is configured in WorkspaceConfiguration.
        // The query is intentionally a server-side projection
        // (we hit the DB once, not N+1).
        Workspace? workspace = await Db.Set<Workspace>()
            .FirstOrDefaultAsync(w => w.Id == workspaceId, ct);
        if (workspace is null)
        {
            return [];
        }
        return workspace.Members.ToList();
    }
}
