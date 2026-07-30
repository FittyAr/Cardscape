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
        return await Task.Run<IReadOnlyList<InboundEmailAddress>>(() =>
        {
            return Db.Set<InboundEmailAddress>().AsEnumerable()
                .Where(a => a.WorkspaceId.Value == workspaceValue && !a.IsDeleted)
                .OrderBy(a => a.CreatedAt)
                .ToList();
        }, ct);
    }

    public async Task<InboundEmailAddress?> FindByEmailAsync(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var needle = email.Trim().ToLowerInvariant();
        return await Task.Run<InboundEmailAddress?>(() =>
        {
            return Db.Set<InboundEmailAddress>().AsEnumerable()
                .Where(a => !a.IsDeleted
                            && a.Active
                            && string.Equals(a.EmailAddress, needle, StringComparison.Ordinal))
                .FirstOrDefault();
        }, ct);
    }
}
