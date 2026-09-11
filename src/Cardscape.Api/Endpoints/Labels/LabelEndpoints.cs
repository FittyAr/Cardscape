using Cardscape.Application.Labels.Commands;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Application.Labels.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Labels;

public static class LabelEndpoints
{
    public static IEndpointRouteBuilder MapLabelEndpoints(this IEndpointRouteBuilder app)
    {
        var boardGroup = app.MapGroup("/api/boards/{boardId:guid}/labels").RequireAuthorization().WithTags("Labels");
        boardGroup.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<LabelDto>>>(new ListLabelsForBoardQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        }).Produces<LabelDto[]>();
        boardGroup.MapPost("/", async (Guid boardId, CreateLabelBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<LabelDto>>(new CreateLabelCommand(boardId, body.Name, body.Color), ct);
            return result.IsSuccess ? Results.Created($"/api/labels/{result.Value.Id}", result.Value) : DomainErrorResults.ToProblem(result.Error);
        }).Produces<LabelDto>(StatusCodes.Status201Created);

        var itemGroup = app.MapGroup("/api/labels").RequireAuthorization().WithTags("Labels");
        itemGroup.MapPut("/{labelId:guid}", async (Guid labelId, UpdateLabelBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<LabelDto>>(new UpdateLabelCommand(labelId, body.Name, body.Color), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        }).Produces<LabelDto>();
        itemGroup.MapDelete("/{labelId:guid}", async (Guid labelId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteLabelCommand(labelId), ct);
            return result.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        return app;
    }

    public sealed record CreateLabelBody(string Name, string Color);
    public sealed record UpdateLabelBody(string Name, string Color);

}
