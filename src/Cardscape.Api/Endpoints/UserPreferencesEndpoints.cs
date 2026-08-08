using Cardscape.Application.UserPreferences.Commands;
using Cardscape.Application.UserPreferences.DTOs;
using Cardscape.Application.UserPreferences.Queries;
using Cardscape.Domain.Common;
using Cardscape.Domain.UserPreferences;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.UserPreferences;

/// <summary>
/// Minimal-API endpoints for the per-user appearance
/// preferences aggregate. Mounted at
/// <c>/api/users/me/preferences</c>. Both endpoints require
/// authentication; the JWT bearer middleware picks them up
/// via the <c>RequireAuthorization()</c> call below.
///
/// Round-trip shape:
///   GET  /api/users/me/preferences
///     200 → UserPreferencesDto
///     404 → { code, message }    (no row yet — call POST)
///     401 → unauthenticated
///   POST /api/users/me/preferences
///     Creates the row with project defaults; idempotent —
///     200 with the DTO if a row already exists.
///   PUT  /api/users/me/preferences
///     Body: { themeName?, mode? }   (either or both)
///     200 → UserPreferencesDto
///     400 → { code, message }    (validation failure)
///     404 → { code, message }    (no row yet — call POST)
///     401 → unauthenticated
/// </summary>
public static class UserPreferencesEndpoints
{
    public static IEndpointRouteBuilder MapUserPreferencesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/me/preferences")
            .RequireAuthorization()
            .WithTags("UserPreferences");

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<UserPreferencesDto?>>(
                new GetUserPreferencesQuery(), ct);
            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            // 200 with null body when the user has no row;
            // the client treats null as the "POST me a
            // default" signal. We deliberately do NOT
            // return 404 here because the GET is
            // idempotent and a fresh user is not an error.
            return result.Value is null
                ? Results.Ok((UserPreferencesDto?)null)
                : Results.Ok(result.Value);
        });

        group.MapPost("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<UserPreferencesDto>>(
                new CreateDefaultUserPreferencesCommand(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapPut("/", async (
            UpdatePreferencesBody body, IMessageBus bus, CancellationToken ct) =>
        {
            // The body sends Mode as a string ("Light" /
            // "Dark" / "System") so the wire contract
            // survives future enum additions. Parse it
            // here; an unknown value surfaces a 400 from
            // the handler via the domain validator.
            AppearanceMode? mode = null;
            if (!string.IsNullOrWhiteSpace(body.Mode))
            {
                if (!Enum.TryParse<AppearanceMode>(body.Mode, ignoreCase: false, out var parsed))
                {
                    return Results.BadRequest(new
                    {
                        code = "members.user_preferences.invalid_mode",
                        message = "Mode must be Light, Dark, or System."
                    });
                }
                mode = parsed;
            }

            var result = await bus.InvokeAsync<Result<UserPreferencesDto>>(
                new UpdateUserPreferencesCommand(body.ThemeName, mode), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        return app;
    }

    /// <summary>Request body for <c>PUT /api/users/me/preferences</c>.
    /// Either or both fields may be null; null means "leave
    /// unchanged".</summary>
    public sealed record UpdatePreferencesBody(string? ThemeName, string? Mode);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
