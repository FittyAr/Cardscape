using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Notifications.Commands;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Application.Notifications.Queries;
using Cardscape.Domain.Common;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for the in-app inbox. AI assistants can list
/// the user's notifications, read them, and mark them as read —
/// useful for "what's on my plate today?" workflows driven by the
/// model.
/// </summary>
[McpServerToolType]
public sealed class InboxTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "inbox_list")]
    public async Task<IReadOnlyList<NotificationDto>> List(
        bool unreadOnly, int skip, int take, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<NotificationDto>>>(
            new ListNotificationsQuery(unreadOnly, skip, take == 0 ? 50 : take), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "inbox_unread_count")]
    public async Task<int> UnreadCount(CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<int>>(new UnreadNotificationsCountQuery(), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "inbox_mark_read")]
    public async Task<string> MarkRead(Guid notificationId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(
            new MarkNotificationReadCommand(notificationId), ct);
        Ensure(result);
        return "read";
    }

    [McpServerTool(Name = "inbox_mark_all_read")]
    public async Task<string> MarkAllRead(CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(new MarkAllNotificationsReadCommand(), ct);
        Ensure(result);
        return "all read";
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT or API token in the Authorization header.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }
    }
}
