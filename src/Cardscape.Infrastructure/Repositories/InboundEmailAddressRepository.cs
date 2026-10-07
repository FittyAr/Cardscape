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
        IQueryable<InboundEmailAddress> query = Set
            .AsNoTracking()
            .Where(address => address.WorkspaceId == workspaceId && !address.IsDeleted);
        return await query.ToListOrderedAsync(Db, address => address.CreatedAt, ct: ct);
    }

    public async Task<InboundEmailAddress?> FindByEmailAsync(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var needle = email.Trim().ToLowerInvariant();
        return await Set
            .FirstOrDefaultAsync(address =>
                !address.IsDeleted
                && address.Active
                && address.EmailAddress == needle, ct);
    }
}
