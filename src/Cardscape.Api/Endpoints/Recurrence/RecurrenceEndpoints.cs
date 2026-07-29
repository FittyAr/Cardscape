using Cardscape.Application.Recurrence;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Recurrence;

public static class RecurrenceEndpoints
{
    public static IEndpointRouteBuilder MapRecurrenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards/{cardId:guid}/recurrence")
            .RequireAuthorization()
            .WithTags("Recurrence");

        group.MapGet("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardRecurrenceDto?>>(
                new GetCardRecurrenceQuery(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPut("/", async (
            Guid cardId, RecurrenceBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardRecurrenceDto>>(
                new SetCardRecurrenceCommand(cardId, body.IntervalDays, body.FirstOccurrenceAt), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DeleteCardRecurrenceCommand(cardId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record RecurrenceBody(int IntervalDays, DateTimeOffset FirstOccurrenceAt);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
