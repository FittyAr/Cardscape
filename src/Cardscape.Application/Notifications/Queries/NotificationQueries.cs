using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Domain.Common;
using MediatR;

namespace Cardscape.Application.Notifications.Queries;

public sealed record ListNotificationsQuery(bool UnreadOnly = false, int Skip = 0, int Take = 50)
    : IRequest<Result<IReadOnlyList<NotificationDto>>>;

public sealed class ListNotificationsQueryHandler(
    INotificationRepository notifications,
    ICurrentUser currentUser) : IRequestHandler<ListNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<NotificationDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await notifications.ListForUserAsync(
            currentUser.Id.Value, request.UnreadOnly, request.Skip, request.Take, cancellationToken);

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

public sealed record UnreadNotificationsCountQuery : IRequest<Result<int>>;

public sealed class UnreadNotificationsCountQueryHandler(
    INotificationRepository notifications,
    ICurrentUser currentUser) : IRequestHandler<UnreadNotificationsCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(
        UnreadNotificationsCountQuery request, CancellationToken cancellationToken)
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
