using Cardscape.Api.Filters;
using Cardscape.Application.Scim;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Scim;

/// <summary>Authenticated admin endpoints for managing per-workspace
/// SCIM tokens. The tokens themselves are presented by the IdP
/// and authenticated by <c>ScimAuthenticationHandler</c> on
/// <c>/scim/v2/...</c>.</summary>
public static class ScimAdminEndpoints
{
    public static IEndpointRouteBuilder MapScimAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/scim")
            .RequireAuthorization()
            .RequireRegionGuard()
            .WithTags("SCIM.Admin");

        group.MapGet("/tokens", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<ScimTokenDto>>>(
                new ListScimTokensQuery(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/tokens", async (
            Guid workspaceId,
            [FromBody] IssueScimTokenBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IssueScimTokenResult>>(
                new IssueScimTokenCommand(workspaceId, body.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/workspaces/{workspaceId}/scim/tokens/{result.Value.Token.Id}",
                                 result.Value)
                : MapError(result.Error);
        });

        group.MapDelete("/tokens/{tokenId:guid}", async (
            Guid workspaceId, Guid tokenId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeScimTokenCommand(workspaceId, tokenId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record IssueScimTokenBody(string Name);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
