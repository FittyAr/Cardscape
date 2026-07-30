using Cardscape.Application.Ai;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Ai;

/// <summary>
/// REST surface for the AI features. The Web UI calls these
/// from the card detail / editor flows. The MCP server has
/// its own MCP-tool wrappers around the same commands.
/// </summary>
public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").RequireAuthorization().WithTags("AI");

        group.MapPost("/cards/{cardId:guid}/generate-description",
            async (Guid cardId, [FromServices] IMessageBus bus, CancellationToken ct) =>
        {
            Result<AiFeatures.AiGeneratedText> result = await bus.InvokeAsync<
                Result<AiFeatures.AiGeneratedText>>(
                new AiFeatures.GenerateCardDescriptionCommand(cardId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/cards/{cardId:guid}/generate-checklist",
            async (Guid cardId, [FromServices] IMessageBus bus, CancellationToken ct) =>
        {
            Result<AiFeatures.AiGeneratedChecklist> result = await bus.InvokeAsync<
                Result<AiFeatures.AiGeneratedChecklist>>(
                new AiFeatures.GenerateChecklistFromDescriptionCommand(cardId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/cards/{cardId:guid}/suggest-owners",
            async (Guid cardId, [FromServices] IMessageBus bus, CancellationToken ct) =>
        {
            Result<AiFeatures.AiOwnerSuggestions> result = await bus.InvokeAsync<
                Result<AiFeatures.AiOwnerSuggestions>>(
                new AiFeatures.SuggestCardOwnersCommand(cardId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/comments/summarize",
            async ([FromBody] SummarizeRequest body, [FromServices] IMessageBus bus, CancellationToken ct) =>
        {
            Result<AiFeatures.AiGeneratedText> result = await bus.InvokeAsync<
                Result<AiFeatures.AiGeneratedText>>(
                new AiFeatures.SummarizeCommentThreadCommand(body.CommentIds ?? []), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        return app;
    }

    public sealed record SummarizeRequest(IReadOnlyList<Guid>? CommentIds);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
