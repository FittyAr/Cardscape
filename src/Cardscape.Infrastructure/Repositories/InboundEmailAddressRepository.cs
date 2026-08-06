using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class InboundEmailAddressRepository(CardscapeDbContext db)
    : RepositoryBase<InboundEmailAddress, InboundEmailAddressId>(db), IInboundEmailAddressRepository
{
    public async Task<IReadOnlyList<InboundEmailAddress>> ListForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        var workspaceValue = workspaceId.Value;
        var rows = new List<InboundEmailAddress>();
        await foreach (var a in Db.Set<InboundEmailAddress>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.WorkspaceId.Value == workspaceValue && !a.IsDeleted)
            {
                rows.Add(a);
            }
        }
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
        await foreach (var a in Db.Set<InboundEmailAddress>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!a.IsDeleted
                && a.Active
                && string.Equals(a.EmailAddress, needle, StringComparison.Ordinal))
            {
                return a;
            }
        }
        return null;
    }
}
