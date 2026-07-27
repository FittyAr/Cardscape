using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// User-specific queries on top of the generic
/// <see cref="IRepository{T, TId}"/>.
/// </summary>
public interface IUserRepository : IRepository<User, UserId>
{
    /// <summary>Returns the user with the given (already lower-cased) email, or <c>null</c>.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
}
