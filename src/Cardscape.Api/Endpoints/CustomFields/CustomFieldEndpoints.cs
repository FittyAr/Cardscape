using Cardscape.Application.CustomFields;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.CustomFields;

public static class CustomFieldEndpoints
{
    public static IEndpointRouteBuilder MapCustomFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/custom-fields")
            .RequireAuthorization()
            .WithTags("Custom fields");

        group.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CustomFieldDefinitionDto>>>(
                new ListCustomFieldDefinitionsQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async (
            Guid boardId,
            CreateFieldBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
                new CreateCustomFieldDefinitionCommand(
                    boardId, body.Name, body.Kind, body.DropdownOptions, body.Position),
                ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/boards/{boardId}/custom-fields/{result.Value.Id}",
                    result.Value)
                : MapError(result.Error);
        });

        group.MapPatch("/{fieldId:guid}", async (
            Guid boardId,
            Guid fieldId,
            RenameFieldBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
                new RenameCustomFieldDefinitionCommand(fieldId, body.NewName), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{fieldId:guid}", async (
            Guid boardId,
            Guid fieldId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DeleteCustomFieldDefinitionCommand(fieldId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    // Nested under /api/cards/{cardId}/custom-field-values for the value side.
    public static IEndpointRouteBuilder MapCustomFieldValueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards/{cardId:guid}/custom-field-values")
            .RequireAuthorization()
            .WithTags("Custom fields");

        group.MapGet("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CustomFieldValueDto>>>(
                new ListCustomFieldValuesForCardQuery(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPut("/{fieldId:guid}", async (
            Guid cardId,
            Guid fieldId,
            SetValueBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CustomFieldValueDto>>(
                new SetCustomFieldValueCommand(cardId, fieldId, body.ValueJson), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public sealed record CreateFieldBody(
        string Name,
        int Kind,
        IReadOnlyList<string>? DropdownOptions,
        int Position = 0);

    public sealed record RenameFieldBody(string NewName);

    public sealed record SetValueBody(string? ValueJson);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
