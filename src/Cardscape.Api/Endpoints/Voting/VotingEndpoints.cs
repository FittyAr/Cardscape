using Cardscape.Application.Voting;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Voting;

public static class VotingEndpoints
{
    public static IEndpointRouteBuilder MapVotingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards/{cardId:guid}/votes")
            .RequireAuthorization()
            .WithTags("Voting");

        // Toggle the current user's vote on the card. Returns
        // the post-toggle state so the Web UI can re-render
        // without a second round trip.
        group.MapPost("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardVoteStateDto>>(
                new ToggleCardVoteCommand(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // Read-only fetch of the current vote state for the card.
        group.MapGet("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardVoteStateDto>>(
                new ListCardVotesQuery(cardId), ct);
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
