using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Notifications;
using MediatR;

namespace Cardscape.Application.Notifications.Commands;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(
        MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var notification = await notifications.GetByIdAsync(new NotificationId(request.NotificationId), cancellationToken);
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

public sealed record MarkAllNotificationsReadCommand : IRequest<Result>;

public sealed class MarkAllNotificationsReadCommandHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    public async Task<Result> Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
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
