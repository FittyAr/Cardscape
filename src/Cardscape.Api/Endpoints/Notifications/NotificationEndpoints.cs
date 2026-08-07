using Cardscape.Application.Notifications.Commands;
using Cardscape.Application.Notifications.DTOs;
using Cardscape.Application.Notifications.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization().WithTags("Notifications");

        // The query has no upper cap on `take` so a
        // malicious caller cannot ask the server to
        // materialise the entire notifications table in
        // one response. The previous endpoint passed the
        // raw `take` through (capped to 0 → 50 default but
        // not to a max); the v1.2.0 audit (pass 12) caps
        // the page at 200 entries per the same constant
        // the SCIM list endpoint uses.
        // All three query parameters are optional. Forcing every consumer
        // (including future MCP clients, scripts, and Scalar's "Try it out"
        // panel) to know the exact query string contract produces 500s from
        // the model binder when any of them is missing — see the BUG #8
        // entry in test-results/BETA-TEST-REPORT.md. Defaults match the
        // ListNotificationsQuery record.
        group.MapGet("/", async (
            bool? unreadOnly,
            int? skip,
            int? take,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            int effectiveTake = take is null or <= 0 ? 50 : Math.Min(take.Value, 200);
            int effectiveSkip = skip is null or < 0 ? 0 : skip.Value;
            var result = await bus.InvokeAsync<Result<IReadOnlyList<NotificationDto>>>(
                new ListNotificationsQuery(unreadOnly ?? false, effectiveSkip, effectiveTake), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapGet("/unread-count", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<int>>(new UnreadNotificationsCountQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(new UnreadCountResponse(result.Value))
                : MapError(result.Error);
        });

        // Returns the unread notification count for the authenticated user.
        // Wrapped in a DTO (rather than a raw int) so the response shape is
        // self-describing and easy to extend with extra counters (e.g. by kind)
        // without breaking the contract.
        // Pinned to the public Cardscape.Api namespace so the native
        // .NET 10+ OpenAPI generator surfaces it under the
        // "Notifications" tag in the document Scalar renders.

        group.MapPost("/mark-all-read", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new MarkAllNotificationsReadCommand(), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapPost("/{notificationId:guid}/read", async (Guid notificationId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new MarkNotificationReadCommand(notificationId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}

/// <summary>Response shape for <c>GET /api/notifications/unread-count</c>.</summary>
public sealed record UnreadCountResponse(int Count);
