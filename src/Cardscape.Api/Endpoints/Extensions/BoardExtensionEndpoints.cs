using Cardscape.Application.Extensions;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Extensions;

/// <summary>
/// REST surface for board extensions. Any board member can list
/// extensions; enabling / disabling / updating the config JSON is
/// also open to any member (v0.6.4 — no admin-only gate yet).
/// </summary>
public static class BoardExtensionEndpoints
{
    public static IEndpointRouteBuilder MapBoardExtensionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/extensions")
            .RequireAuthorization()
            .WithTags("Extensions");

        group.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardExtensionDto>>>(
                new ListBoardExtensionsQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async (
            Guid boardId,
            EnableExtensionBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardExtensionDto>>(
                new EnableBoardExtensionCommand(boardId, body.Kind, body.ConfigJson), ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/boards/{boardId}/extensions/{body.Kind}",
                    result.Value)
                : MapError(result.Error);
        });

        group.MapDelete("/{kind:int}", async (Guid boardId, int kind, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DisableBoardExtensionCommand(boardId, kind), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapPut("/{kind:int}/config", async (
            Guid boardId,
            int kind,
            UpdateConfigBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardExtensionDto>>(
                new UpdateBoardExtensionConfigCommand(boardId, kind, body.ConfigJson), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public sealed record EnableExtensionBody(int Kind, string? ConfigJson);
    public sealed record UpdateConfigBody(string? ConfigJson);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
