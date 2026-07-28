using Cardscape.Application.Common;
using Cardscape.Application.Security.Commands;
using Cardscape.Application.Security.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Security;

public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/security/api-tokens").RequireAuthorization().WithTags("Security");

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<ApiTokenSummaryDto>>>(
                new ListApiTokensForUserQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/", async (IssueApiTokenBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ApiTokenIssuanceDto>>(
                new IssueApiTokenCommand(body.Name, body.Scopes, body.ExpiresAt, body.RateLimitPerHour, body.BurstSize),
                ct);
            return result.IsSuccess
                ? Results.Created($"/api/security/api-tokens/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapDelete("/{tokenId:guid}", async (Guid tokenId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeApiTokenCommand(tokenId, null), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{tokenId:guid}/revoke", async (Guid tokenId, RevokeApiTokenBody? body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeApiTokenCommand(tokenId, body?.Reason), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPatch("/{tokenId:guid}/rate-limit", async (Guid tokenId, UpdateRateLimitBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ApiTokenRateLimitDto>>(
                new UpdateApiTokenRateLimitCommand(tokenId, body.RateLimitPerHour, body.BurstSize), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapGet("/{tokenId:guid}/rate-limit-status", async (Guid tokenId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ApiTokenRateLimitStatusDto>>(
                new GetApiTokenRateLimitStatusQuery(tokenId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        return app;
    }

    public sealed record IssueApiTokenBody(
        string Name,
        IReadOnlyCollection<string> Scopes,
        DateTimeOffset? ExpiresAt,
        int? RateLimitPerHour = null,
        int? BurstSize = null);

    public sealed record RevokeApiTokenBody(string? Reason);

    public sealed record UpdateRateLimitBody(int RateLimitPerHour, int BurstSize);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
