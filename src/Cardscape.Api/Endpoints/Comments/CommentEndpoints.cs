using Cardscape.Application.Comments.Commands;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Application.Comments.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Comments;

public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards/{cardId:guid}/comments").RequireAuthorization().WithTags("Comments");

        group.MapGet("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CommentDto>>>(new ListCommentsForCardQuery(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async (Guid cardId, AddCommentBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CommentDto>>(new AddCommentCommand(cardId, body.Body), ct);
            return result.IsSuccess ? Results.Created($"/api/cards/{cardId}/comments/{result.Value.Id}", result.Value) : MapError(result.Error);
        });

        // BETA-7-#8 — see test-results/BETA-TEST-REPORT.md.
        // Edit and delete used to live under
        // `/api/comments/{commentId}` while add lived
        // under `/api/cards/{cardId}/comments`. The
        // inconsistent path shape was a footgun. Both
        // routes are now under the same parent so a
        // client only has to remember one path template.
        // The legacy `/api/comments/{commentId}` routes
        // remain as `[Obsolete]`-style no-ops behind a
        // sibling group so existing callers don't break.
        group.MapPut("/{commentId:guid}", async (Guid cardId, Guid commentId, EditCommentBody body, IMessageBus bus, CancellationToken ct) =>
        {
            _ = cardId; // path-anchored for consistency; the comment carries its own cardId.
            var result = await bus.InvokeAsync<Result<CommentDto>>(new EditCommentCommand(commentId, body.NewBody), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{commentId:guid}", async (Guid cardId, Guid commentId, IMessageBus bus, CancellationToken ct) =>
        {
            _ = cardId;
            var result = await bus.InvokeAsync<Result>(new DeleteCommentCommand(commentId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        // Legacy routes — kept for back-compat. The
        // existing `/api/comments/{commentId}` PUT and
        // DELETE redirect to the new
        // `/api/cards/{cardId}/comments/{commentId}`
        // route. The cardId is resolved from the
        // comment row inside the handler so the legacy
        // caller doesn't need to know it.
        var legacyGroup = app.MapGroup("/api/comments").RequireAuthorization().WithTags("Comments");

        legacyGroup.MapPut("/{commentId:guid}", async (Guid commentId, EditCommentBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CommentDto>>(new EditCommentCommand(commentId, body.NewBody), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        legacyGroup.MapDelete("/{commentId:guid}", async (Guid commentId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteCommentCommand(commentId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record AddCommentBody(string Body);
    public sealed record EditCommentBody(string NewBody);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
