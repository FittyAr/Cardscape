using Cardscape.Domain.Notifications;

namespace Cardscape.Application.Abstractions.Persistence;

public interface INotificationRepository : IRepository<Notification, NotificationId>
{
    Task<IReadOnlyList<Notification>> ListForUserAsync(Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
}
