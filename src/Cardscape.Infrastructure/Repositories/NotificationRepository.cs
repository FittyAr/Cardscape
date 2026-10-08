using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Notifications;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class NotificationRepository(CardscapeDbContext db) : RepositoryBase<Notification, NotificationId>(db), INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> ListForUserAsync(
        Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct = default)
    {
        IQueryable<Notification> query = Set
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        return await query.ToListOrderedAsync(
            Db, notification => notification.CreatedAt, descending: true, skip, take, ct);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        await Set.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<int> MarkAllReadAsync(
        Guid userId, DateTimeOffset readAt, CancellationToken ct = default) =>
        await Set
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true)
                .SetProperty(notification => notification.ReadAt, readAt)
                .SetProperty(notification => notification.UpdatedAt, readAt)
                .SetProperty(notification => notification.UpdatedBy, (Guid?)null)
                .SetProperty(notification => notification.RowVersion, notification => notification.RowVersion + 1),
                ct);
}
