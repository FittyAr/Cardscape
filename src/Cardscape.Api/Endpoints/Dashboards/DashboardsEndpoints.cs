using Cardscape.Application.Dashboards.Commands;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Application.Dashboards.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Dashboards;

public static class DashboardsEndpoints
{
    public static IEndpointRouteBuilder MapDashboardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/dashcards")
            .RequireAuthorization()
            .WithTags("Dashcards");

        group.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<DashcardDto>>>(
                new ListDashcardsForBoardQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async ([FromBody] CreateDashcardRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<DashcardDto>>(new CreateDashcardCommand(
                body.BoardId, body.Kind, body.Title, body.ConfigurationJson, body.Position), ct);
            return result.IsSuccess
                ? Results.Created($"/api/boards/{body.BoardId}/dashcards/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapPut("/{dashcardId:guid}/config", async (
            Guid dashcardId, [FromBody] UpdateDashcardConfigRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<DashcardDto>>(
                new UpdateDashcardConfigCommand(dashcardId, body.ConfigurationJson), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{dashcardId:guid}", async (Guid dashcardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteDashcardCommand(dashcardId), ct);
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
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
