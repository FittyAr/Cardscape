using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class GoogleCalendarConnectionRepository(
    CardscapeDbContext db) : IGoogleCalendarConnectionRepository
{
    public Task<GoogleCalendarConnection?> FindByUserAsync(Guid userId, CancellationToken ct = default) =>
        db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public Task<GoogleCalendarConnection?> FindByIdAsync(GoogleCalendarConnectionId id, CancellationToken ct = default) =>
        db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(GoogleCalendarConnection connection, CancellationToken ct = default)
    {
        await db.GoogleCalendarConnections.AddAsync(connection, ct);
    }
}
