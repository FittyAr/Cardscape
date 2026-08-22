using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class InboundEmailAddressRepository(CardscapeDbContext db)
    : RepositoryBase<InboundEmailAddress, InboundEmailAddressId>(db), IInboundEmailAddressRepository
{
    public async Task<IReadOnlyList<InboundEmailAddress>> ListForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        IQueryable<InboundEmailAddress> query = Db.Set<InboundEmailAddress>()
            .AsNoTracking()
            .Where(address => address.WorkspaceId == workspaceId && !address.IsDeleted);
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderBy(address => address.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }

    public async Task<InboundEmailAddress?> FindByEmailAsync(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var needle = email.Trim().ToLowerInvariant();
        return await Db.Set<InboundEmailAddress>()
            .FirstOrDefaultAsync(address =>
                !address.IsDeleted
                && address.Active
                && address.EmailAddress == needle, ct);
    }
}
