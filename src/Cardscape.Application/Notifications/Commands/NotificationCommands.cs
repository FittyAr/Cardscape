using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Notifications;
using Wolverine;

namespace Cardscape.Application.Notifications.Commands;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IMessage;

public static class MarkNotificationReadCommandHandler
{
    public static async Task<Result> Handle(
        MarkNotificationReadCommand command,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var notification = await notifications.GetByIdAsync(new NotificationId(command.NotificationId), cancellationToken);
        if (notification is null)
        {
            return Result.Failure(DomainError.NotFound(
                "notifications.not_found", "Notification was not found."));
        }

        if (notification.UserId != currentUser.Id.Value)
        {
            return Result.Failure(DomainError.Forbidden(
                "notifications.forbidden", "You cannot modify this notification."));
        }

        notification.MarkRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record MarkAllNotificationsReadCommand : IMessage;

public static class MarkAllNotificationsReadCommandHandler
{
    public static async Task<Result> Handle(
        MarkAllNotificationsReadCommand command,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var unread = await notifications.ListForUserAsync(
            currentUser.Id.Value, unreadOnly: true, skip: 0, take: 200, cancellationToken);

        foreach (var n in unread)
        {
            n.MarkRead(clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
