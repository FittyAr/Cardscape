using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="Domain.UserPreferences.UserPreferences"/>.
/// The primary key is the owning <see cref="UserId"/>, so
/// the standard <c>GetByIdAsync(UserId)</c> lookup is the
/// common case. The <see cref="DeleteByUserIdAsync"/>
/// shortcut exists for the GDPR paths
/// (<c>SoftDeleteUserCommandHandler</c> /
/// <c>AnonymiseUserCommandHandler</c>) which need to drop
/// the row without a prior read.
/// </summary>
public interface IUserPreferencesRepository : IRepository<Domain.UserPreferences.UserPreferences, UserId>
{
    /// <summary>Hard-deletes the preferences row for the given
    /// user. No-op if the user has no row. Called from the
    /// GDPR user-lifecycle command handlers; the framework's
    /// soft-delete flag on the <c>users</c> table is the
    /// authoritative "is the user gone" signal — the
    /// preferences row is hard-deleted unconditionally.</summary>
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
}
