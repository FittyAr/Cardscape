using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Integrations.GoogleCalendar;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Integrations;

/// <summary>Google Calendar integration REST endpoints.</summary>
public static class GoogleCalendarEndpoints
{
    public static IEndpointRouteBuilder MapGoogleCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/google-calendar")
            .RequireAuthorization()
            .WithTags("Integrations.GoogleCalendar");

        // The connect endpoint accepts the encrypted refresh token
        // (encrypted client-side — the browser never sees the clear
        // token after the initial OAuth exchange).
        group.MapPost("/connect", async (
            [FromBody] ConnectGoogleCalendarRequest body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<GoogleCalendarConnectionDto>>(
                new EstablishGoogleCalendarConnectionCommand(
                    body.WorkspaceId,
                    body.GoogleEmail,
                    body.EncryptedRefreshToken,
                    body.CalendarId ?? "primary"),
                ct);
            return result.IsSuccess
                ? Results.Created("/api/integrations/google-calendar", result.Value)
                : MapError(result.Error);
        });

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<GoogleCalendarConnectionDto?>>(
                new GetGoogleCalendarConnectionQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapDelete("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeGoogleCalendarConnectionCommand(), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record ConnectGoogleCalendarRequest(
        Guid WorkspaceId,
        string GoogleEmail,
        string EncryptedRefreshToken,
        string? CalendarId);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.External => Results.Json(new { error.Code, error.Message }, statusCode: StatusCodes.Status502BadGateway),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
