using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Integrations.GoogleCalendar;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<GoogleCalendarConnectionDto?>>(
                new GetGoogleCalendarConnectionQuery(), ct);
            if (!result.IsSuccess)
            {
                return MapError(result.Error);
            }

            // BETA-2-UI-#6 — see test-results/ui/beta-test-r2-ui.md.
            //
            // The previous handler returned `Results.Ok(result.Value)`
            // where `result.Value` was a nullable `GoogleCalendarConnectionDto?`.
            // When the user had not connected a Google Calendar, the
            // value was `null` and `Results.Ok(null)` serialised a
            // 0-byte body. The Blazor WASM client
            // (`GoogleCalendarApiClient.GetConnectionAsync`) called
            // `ReadFromJsonAsync<GoogleCalendarConnectionDto>(...)` on
            // the empty response and threw `JsonException:
            // ExpectedJsonTokens`, leaving the page stuck on "Loading…".
            // The fix is to always return a non-null body so the
            // client can deserialise a default "not connected" state
            // and render the connect form.
            if (result.Value is null)
            {
                return Results.Ok(new GoogleCalendarConnectionDto(
                    Id: Guid.Empty,
                    UserId: Guid.Empty,
                    WorkspaceId: Guid.Empty,
                    GoogleEmail: string.Empty,
                    CalendarId: string.Empty,
                    LastSyncedAt: null,
                    LastSyncErrorAt: null,
                    LastSyncError: null,
                    IsActive: false));
            }

            return Results.Ok(result.Value);
        });

        group.MapDelete("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeGoogleCalendarConnectionCommand(), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

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
