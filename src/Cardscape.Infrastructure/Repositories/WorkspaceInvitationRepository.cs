using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceInvitationRepository"/>.
/// The lookup-by-hash path is the accept endpoint's hot path: every
/// accept attempt hashes the cleartext and looks it up. The
/// email-scoped query streams client-side because the strongly-typed
/// <c>Email</c> value object can't be translated to SQL through the
/// conversion (same trap as <see cref="ApiTokenRepository"/>).
/// </summary>
public sealed class WorkspaceInvitationRepository(CardscapeDbContext db)
    : RepositoryBase<WorkspaceInvitation, WorkspaceInvitationId>(db), IWorkspaceInvitationRepository
{
    public async Task<WorkspaceInvitation?> FindByTokenHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        return await Db.Set<WorkspaceInvitation>()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
    }

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListForWorkspaceAsync(
        Guid workspaceId, bool includeTerminal, CancellationToken ct = default)
    {
        var query = Db.Set<WorkspaceInvitation>()
            .Where(i => i.WorkspaceId.Value == workspaceId);

        if (!includeTerminal)
        {
            query = query.Where(i => i.AcceptedAt == null && i.RevokedAt == null);
        }

        var rows = await query
            .OrderByDescending(i => i.InvitedAt)
            .ToListAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListPendingForEmailAsync(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        var normalized = email.Trim().ToLowerInvariant();
        // Email is a value object; the strongly-typed access path
        // doesn't translate. Stream client-side and filter, then
        // keep only non-terminal rows in memory.
        var rows = new List<WorkspaceInvitation>();
        await foreach (var inv in Db.Set<WorkspaceInvitation>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!string.Equals(inv.Email, normalized, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (inv.AcceptedAt is not null || inv.RevokedAt is not null)
            {
                continue;
            }

            rows.Add(inv);
        }

        rows.Sort((a, b) => b.InvitedAt.CompareTo(a.InvitedAt));
        return rows;
    }
}
