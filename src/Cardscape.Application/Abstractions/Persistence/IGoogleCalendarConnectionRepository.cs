using Cardscape.Domain.Integrations.GoogleCalendar;

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
}
