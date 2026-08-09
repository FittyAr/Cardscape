using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Security;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class ApiTokenRepository(CardscapeDbContext db)
    : RepositoryBase<ApiToken, ApiTokenId>(db), IApiTokenRepository
{
    public async Task<ApiToken?> FindByHashedSecretAsync(string hashedSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hashedSecret))
        {
            return null;
        }

        return await Db.Set<ApiToken>()
            .FirstOrDefaultAsync(t => t.HashedSecret == hashedSecret, ct);
    }

    public async Task<IReadOnlyList<ApiToken>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // EF Core 10 can't translate the strongly-typed-id value-object
        // access path on the UserId navigation when combined with
        // HasConversion. Stream client-side, filter, and order
        // in memory. The result set is bounded by the number of
        // tokens one user owns, which is small in practice.
        var rows = new List<ApiToken>();
        await foreach (var token in Db.Set<ApiToken>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (token.UserId.Value != userId)
            {
                continue;
            }

            rows.Add(token);
        }

        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }
}
