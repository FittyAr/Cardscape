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
            if (!result.IsSuccess)
            {
                return MapError(result.Error);
            }

            // BETA-6-#3 — see test-results/BETA-TEST-REPORT.md.
            // The previous implementation returned 404 when the
            // card has no recurrence row. The Blazor client does
            // treat 404 as "no recurrence" (so the page renders
            // correctly) but the browser console logs every 404
            // as a noisy error, which makes real errors harder
            // to spot in the dev tools. 204 No Content is the
            // idiomatic REST signal for "exists, but no
            // representation" and doesn't show up as a red
            // network error. The Blazor client now treats both
            // 204 and 404 as the "no recurrence" path.
            return result.Value is null
                ? Results.NoContent()
                : Results.Ok(result.Value);
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
