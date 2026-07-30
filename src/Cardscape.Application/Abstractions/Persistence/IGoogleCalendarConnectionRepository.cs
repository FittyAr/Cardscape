using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Repository for <see cref="GoogleCalendarConnection"/>.
/// One connection per user (the per-user invariant is enforced
/// in the command handler — see
/// <c>EstablishGoogleCalendarConnectionCommandHandler</c>).</summary>
public interface IGoogleCalendarConnectionRepository
{
    Task<GoogleCalendarConnection?> FindByUserAsync(Guid userId, CancellationToken ct = default);
    Task<GoogleCalendarConnection?> FindByIdAsync(GoogleCalendarConnectionId id, CancellationToken ct = default);
    Task AddAsync(GoogleCalendarConnection connection, CancellationToken ct = default);

    /// <summary>Every active <see cref="GoogleCalendarConnection"/>
    /// attached to a workspace. Used by the
    /// <c>CardDueDateChangedEventHandler</c> push path to fan
    /// a single card update out to the calendar of every user
    /// who has connected their Google account for the
    /// workspace.</summary>
    Task<IReadOnlyList<GoogleCalendarConnection>> ListActiveForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default);

    /// <summary>Persists changes to a previously-loaded
    /// connection (e.g. updated <c>SyncToken</c>,
    /// <c>ChannelId</c>, or <c>RecordSyncSuccess /
    /// RecordSyncError</c> side-effects). The default
    /// implementation relies on the EF Core change tracker, so
    /// the caller just needs the loaded aggregate to push the
    /// changes through <c>SaveChangesAsync</c>.</summary>
    Task UpdateAsync(GoogleCalendarConnection connection, CancellationToken ct = default);
}
