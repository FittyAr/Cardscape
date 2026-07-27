using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class UserRepository(CardscapeDbContext db) : RepositoryBase<User, UserId>(db), IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        await Db.Set<User>().FirstOrDefaultAsync(u => u.Email.Value == email, ct);
}
