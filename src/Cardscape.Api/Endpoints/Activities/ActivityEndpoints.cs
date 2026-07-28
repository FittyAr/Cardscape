using Cardscape.Application.Activities.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Activities;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var boardGroup = app.MapGroup("/api/boards/{boardId:guid}/activities")
            .RequireAuthorization()
            .WithTags("Activities");

        boardGroup.MapGet("/", async (
            Guid boardId,
            string? cursor,
            int? limit,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ActivityPage>>(
                new ListBoardActivitiesQuery(boardId, cursor, limit), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        var cardGroup = app.MapGroup("/api/cards/{cardId:guid}/activities")
            .RequireAuthorization()
            .WithTags("Activities");

        cardGroup.MapGet("/", async (
            Guid cardId,
            string? cursor,
            int? limit,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ActivityPage>>(
                new ListCardActivitiesQuery(cardId, cursor, limit), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
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
