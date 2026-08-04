using Cardscape.Application.Users.Queries;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Builds the GDPR Art. 15 right-of-access export
/// bundle for a user. The service is the read-side
/// counterpart of the user-lifecycle commands; it
/// reads every row in every table where the user is
/// the subject and projects the result to the
/// <see cref="UserDataExportDto"/> shape. The
/// implementation lives in
/// <c>src/Cardscape.Infrastructure/Persistence/UserDataExportService.cs</c>.
/// </summary>
public interface IUserDataExportService
{
    /// <summary>Returns <c>null</c> if the user does not exist.</summary>
    Task<UserDataExportDto?> BuildExportAsync(UserId userId, CancellationToken ct = default);
}
