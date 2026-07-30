using Cardscape.Application.Common;
using Cardscape.Application.OAuth.Commands;
using Cardscape.Application.OAuth.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.OAuth;

/// <summary>
/// User-facing endpoints for managing OAuth 3rd-party app
/// registrations. Distinct from <see cref="OAuthFlowEndpoints"/>
/// which serve the OAuth 2.0 protocol itself.
/// </summary>
public static class OAuthAppEndpoints
{
    public static IEndpointRouteBuilder MapOAuthAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/oauth-apps").RequireAuthorization().WithTags("OAuthApps");

        group.MapGet("/", async ([FromServices] IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<OAuthAppSummaryDto>>>(
                new ListOAuthAppsForOwnerQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/", async (
            [FromBody] RegisterOAuthAppBody body,
            [FromServices] IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<OAuthAppRegistrationDto>>(
                new RegisterOAuthAppCommand(
                    body.Name,
                    body.AllowedScopes ?? [],
                    body.RedirectUris ?? []),
                ct);
            return result.IsSuccess
                ? Results.Created($"/api/oauth-apps/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapDelete("/{appId:guid}", async (
            Guid appId,
            [FromServices] IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeOAuthAppCommand(appId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        return app;
    }

    public sealed record RegisterOAuthAppBody(
        string Name,
        IReadOnlyCollection<string>? AllowedScopes,
        IReadOnlyCollection<string>? RedirectUris);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
