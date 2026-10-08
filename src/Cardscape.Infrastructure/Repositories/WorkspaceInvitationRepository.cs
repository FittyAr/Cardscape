using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceInvitationRepository"/>.
/// The lookup-by-hash path is the accept endpoint's hot path: every
/// accept attempt hashes the cleartext and looks it up. The
/// email-scoped query uses the normalized indexed string directly.
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

        return await Set
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
    }

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListForWorkspaceAsync(
        Guid workspaceId, bool includeTerminal, CancellationToken ct = default)
    {
        IQueryable<WorkspaceInvitation> query = Set
            .AsNoTracking()
            .Where(invitation => invitation.WorkspaceId == new WorkspaceId(workspaceId));
        if (!includeTerminal)
        {
            query = query.Where(invitation => invitation.AcceptedAt == null && invitation.RevokedAt == null);
        }
        return await query.ToListOrderedAsync(Db, invitation => invitation.InvitedAt, descending: true, ct: ct);
    }

    public async Task<IReadOnlyList<WorkspaceInvitation>> ListPendingForEmailAsync(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        var normalized = EmailAddress.Normalize(email);
        IQueryable<WorkspaceInvitation> query = Set
            .AsNoTracking()
            .Where(invitation =>
                invitation.Email == normalized
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null);
        return await query.ToListOrderedAsync(Db, invitation => invitation.InvitedAt, descending: true, ct: ct);
    }
}
