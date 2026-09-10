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
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        group.MapPost("/", async (Guid cardId, AddCommentBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CommentDto>>(new AddCommentCommand(cardId, body.Body), ct);
            return result.IsSuccess ? Results.Created($"/api/cards/{cardId}/comments/{result.Value.Id}", result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        group.MapPut("/{commentId:guid}", async (Guid cardId, Guid commentId, EditCommentBody body, IMessageBus bus, CancellationToken ct) =>
        {
            _ = cardId; // path-anchored for consistency; the comment carries its own cardId.
            var result = await bus.InvokeAsync<Result<CommentDto>>(new EditCommentCommand(commentId, body.NewBody), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        group.MapDelete("/{commentId:guid}", async (Guid cardId, Guid commentId, IMessageBus bus, CancellationToken ct) =>
        {
            _ = cardId;
            var result = await bus.InvokeAsync<Result>(new DeleteCommentCommand(commentId), ct);
            return result.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(result.Error);
        });

        return app;
    }

    public sealed record AddCommentBody(string Body);
    public sealed record EditCommentBody(string NewBody);

}
