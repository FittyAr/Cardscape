using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class NotificationRepository(CardscapeDbContext db) : RepositoryBase<Notification, NotificationId>(db), INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> ListForUserAsync(
        Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct = default)
    {
        var query = Db.Set<Notification>().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        await Db.Set<Notification>().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
}
