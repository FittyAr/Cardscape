using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Notifications.Queries;

public sealed record ListNotificationsQuery(bool UnreadOnly = false, int Skip = 0, int Take = 50)
    : IMessage;

public static class ListNotificationsQueryHandler
{
    public static async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        ListNotificationsQuery query,
        INotificationRepository notifications,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<NotificationDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await notifications.ListForUserAsync(
            currentUser.Id.Value, query.UnreadOnly, query.Skip, query.Take, cancellationToken);

        var rows = items
            .Select(n => new NotificationDto(
                n.Id.Value,
                n.UserId,
                n.Kind.ToString(),
                n.PayloadJson,
                n.IsRead,
                n.ReadAt,
                n.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<NotificationDto>>(rows);
    }
}

public sealed record UnreadNotificationsCountQuery : IMessage;

public static class UnreadNotificationsCountQueryHandler
{
    public static async Task<Result<int>> Handle(
        UnreadNotificationsCountQuery query,
        INotificationRepository notifications,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<int>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var count = await notifications.CountUnreadAsync(currentUser.Id.Value, cancellationToken);
        return Result.Success(count);
    }
}
