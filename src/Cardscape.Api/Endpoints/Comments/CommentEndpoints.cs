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
            return result.IsSuccess ? Results.Created($"/api/comments/{result.Value.Id}", result.Value) : MapError(result.Error);
        });

        var itemGroup = app.MapGroup("/api/comments").RequireAuthorization().WithTags("Comments");

        itemGroup.MapPut("/{commentId:guid}", async (Guid commentId, EditCommentBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CommentDto>>(new EditCommentCommand(commentId, body.NewBody), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        itemGroup.MapDelete("/{commentId:guid}", async (Guid commentId, IMessageBus bus, CancellationToken ct) =>
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
