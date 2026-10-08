using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class UserRepository(CardscapeDbContext db) : RepositoryBase<User, UserId>(db), IUserRepository
{
    public async Task<User?> FindByEmailVerificationTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(tokenHash)
            ? null
            : await Set.FirstOrDefaultAsync(user => user.EmailVerificationTokenHash == tokenHash, ct);

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = EmailAddress.Normalize(email);
        if (normalized.Length == 0)
        {
            return null;
        }

        EmailAddress typedEmail = EmailAddress.Create(normalized).Value;
        return await Set.FirstOrDefaultAsync(user => user.Email == typedEmail, ct);
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
        HashSet<UserId> wanted = [.. ids];
        return await Set
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
            ? await Set.FirstOrDefaultAsync(user => user.Id == userId, ct)
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
        IQueryable<User> query = Set
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

    public async Task<IReadOnlyList<User>> ListForAdministrationAsync(
        UserStatusFilter status, CancellationToken ct = default)
    {
        IQueryable<User> query = status switch
        {
            UserStatusFilter.Active => Set.Where(user => user.IsActive && !user.IsDeleted && !user.IsAnonymised),
            UserStatusFilter.Deactivated => Set.Where(user => !user.IsActive && !user.IsDeleted && !user.IsAnonymised),
            UserStatusFilter.Deleted => Set.Where(user => user.IsDeleted || user.IsAnonymised),
            _ => Set
        };

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task<int> CountActiveAdminsAsync(CancellationToken ct = default) =>
        await Set.CountAsync(user => user.IsAdmin && user.IsActive && !user.IsDeleted && !user.IsAnonymised, ct);

    public async Task<bool> AnyAsync(CancellationToken ct = default) =>
        await Set.AnyAsync(ct);
}

