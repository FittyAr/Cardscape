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
        // SQLite does not support ORDER BY on DateTimeOffset columns.
        // Push the user filter to the server, materialize, then sort
        // and paginate in memory. The inbox is per-user, so the row
        // count is bounded by recent activity.
        var query = Db.Set<Notification>().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var rows = new List<Notification>();
        await foreach (var n in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            rows.Add(n);
        }

        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows.Skip(skip).Take(take).ToList();
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        await Db.Set<Notification>().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
}
