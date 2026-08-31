using System.Linq;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



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

        EmailAddress typedEmail = EmailAddress.Create(normalized).Value;
        return await Db.Set<User>().FirstOrDefaultAsync(user => user.Email == typedEmail, ct);
    }


    public async Task<IReadOnlyList<User>> ListByIdsAsync(
        IReadOnlyList<UserId> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        // EF Core can translate the strongly-typed id to SQL via
        // the value-object converter; the Contains call is the
        // simplest batching primitive the relational provider
        // supports and avoids an N+1 round-trip when a list
        // projection (comments, activities, etc.) needs the
        // display name for every distinct author.
        HashSet<UserId> wanted = new(ids);
        return await Db.Set<User>()
            .Where(u => wanted.Contains(u.Id))
            .ToListAsync(ct);
    }

    public async Task<User?> FindWorkspaceUserAsync(
        WorkspaceId workspaceId,
        UserId userId,
        CancellationToken ct = default)
    {
        bool isMember = await Db.Set<Workspace>()
            .AnyAsync(workspace =>
                workspace.Id == workspaceId
                && workspace.Members.Any(member => member.UserId == userId.Value),
                ct);

        return isMember
            ? await Db.Set<User>().FirstOrDefaultAsync(user => user.Id == userId, ct)
            : null;
    }

    public async Task<IReadOnlyList<User>> ListWorkspaceUsersAsync(
        WorkspaceId workspaceId,
        string? normalizedEmail,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        List<Guid> memberIds = await Db.Set<Workspace>()
            .Where(workspace => workspace.Id == workspaceId)
            .SelectMany(workspace => workspace.Members)
            .Select(member => member.UserId)
            .ToListAsync(ct);
        HashSet<UserId> typedMemberIds = memberIds
            .Select(memberId => new UserId(memberId))
            .ToHashSet();
        IQueryable<User> query = Db.Set<User>()
            .Where(user => typedMemberIds.Contains(user.Id));
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            EmailAddress email = EmailAddress.Create(normalizedEmail).Value;
            query = query.Where(user => user.Email == email);
        }

        return await query
            .OrderBy(user => user.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
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
