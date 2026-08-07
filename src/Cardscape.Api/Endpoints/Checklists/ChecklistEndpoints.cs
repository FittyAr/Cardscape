using Cardscape.Application.Checklists;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Checklists;

public static class ChecklistEndpoints
{
    public static IEndpointRouteBuilder MapChecklistEndpoints(this IEndpointRouteBuilder app)
    {
        var cardGroup = app.MapGroup("/api/cards/{cardId:guid}/checklists")
            .RequireAuthorization()
            .WithTags("Checklists");

        cardGroup.MapGet("/", async (
            Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<ChecklistDto>>>(
                new ListCardChecklistsQuery(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        cardGroup.MapPost("/", async (
            Guid cardId, CreateChecklistBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new CreateChecklistCommand(cardId, body.Title), ct);
            return result.IsSuccess ? Results.Created($"/api/checklists/{result.Value.Id}", result.Value) : MapError(result.Error);
        });

        var itemGroup = app.MapGroup("/api/checklists/{checklistId:guid}")
            .RequireAuthorization()
            .WithTags("Checklists");

        itemGroup.MapPatch("/", async (
            Guid checklistId, RenameChecklistBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new RenameChecklistCommand(checklistId, body.Title), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        itemGroup.MapDelete("/", async (
            Guid checklistId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DeleteChecklistCommand(checklistId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        itemGroup.MapPost("/items/", async (
            Guid checklistId, AddItemBody body, IMessageBus bus, CancellationToken ct) =>
        {
            // BETA-8-API-#3 — see test-results/r8/r8-report.md.
            // The endpoint used to return the full ChecklistDto
            // (everything + the freshly-added item buried in
            // `items[]`); the canonical REST shape for a POST
            // that creates a single resource is the resource
            // itself. The handler now returns ChecklistItemDto.
            var result = await bus.InvokeAsync<Result<ChecklistItemDto>>(
                new AddChecklistItemCommand(checklistId, body.Text), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        itemGroup.MapPatch("/items/{itemId:guid}/toggle", async (
            Guid checklistId, Guid itemId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new ToggleChecklistItemCommand(checklistId, itemId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        itemGroup.MapPatch("/items/{itemId:guid}/rename", async (
            Guid checklistId, Guid itemId, RenameItemBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new RenameChecklistItemCommand(checklistId, itemId, body.Text), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        itemGroup.MapDelete("/items/{itemId:guid}", async (
            Guid checklistId, Guid itemId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new DeleteChecklistItemCommand(checklistId, itemId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public sealed record CreateChecklistBody(string Title);
    public sealed record RenameChecklistBody(string Title);
    public sealed record AddItemBody(string Text);
    public sealed record RenameItemBody(string Text);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
