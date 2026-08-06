using Cardscape.Application.Lists.Commands;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Lists.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Lists;

public static class ListEndpoints
{
    public static IEndpointRouteBuilder MapListEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lists").RequireAuthorization().WithTags("Lists");

        // BETA-5-#8 — see test-results/BETA-TEST-REPORT.md.
        //
        // The endpoint previously bound `includeArchived` as a
        // required parameter, so every call had to send
        // `?boardId=...&includeArchived=...` even though the
        // field has a sensible default (`false`). The
        // ListListsForBoardQuery record has
        // `IncludeArchived = false` baked in; binding the
        // endpoint parameter to a default-valued local and
        // forwarding keeps the same handler signature while
        // making the parameter optional at the HTTP layer.
        group.MapGet("/", async (
            Guid boardId,
            [FromQuery] bool? includeArchived,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardListDto>>>(
                new ListListsForBoardQuery(boardId, includeArchived ?? false), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapGet("/{listId:guid}", async (Guid listId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardListDto>>(new GetListQuery(listId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async (CreateListBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardListDto>>(new CreateListCommand(body.BoardId, body.Name), ct);
            return result.IsSuccess ? Results.Created($"/api/lists/{result.Value.Id}", result.Value) : MapError(result.Error);
        });

        group.MapPost("/{listId:guid}/rename", async (Guid listId, RenameBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardListDto>>(new RenameListCommand(listId, body.NewName), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{listId:guid}/move", async (Guid listId, MoveBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardListDto>>(new MoveListCommand(listId, body.NewPosition), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{listId:guid}/archive", async (Guid listId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardListDto>>(new ArchiveListCommand(listId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{listId:guid}/restore", async (Guid listId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardListDto>>(new RestoreListCommand(listId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public sealed record CreateListBody(Guid BoardId, string Name);
    public sealed record RenameBody(string NewName);
    public sealed record MoveBody(double NewPosition);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
