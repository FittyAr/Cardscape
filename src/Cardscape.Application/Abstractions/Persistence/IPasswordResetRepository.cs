using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IPasswordResetRepository : IRepository<PasswordReset, PasswordResetId>
{
    Task<PasswordReset?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
