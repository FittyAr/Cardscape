using Cardscape.Application.Abstractions;
using Cardscape.Application.Webhooks;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Webhooks;

/// <summary>
/// BETA-5-#3 — see test-results/BETA-TEST-REPORT.md.
///
/// The webhook domain layer (entities, repository, broadcaster,
/// delivery handler, command/query handlers) was already in
/// place — but no HTTP endpoint exposed any of it. This file
/// adds the surface the Web UI needs: list/create/update/delete
/// endpoints scoped to a board, plus a deliveries view so the
/// Web UI can show the last N attempts (status, attempt count,
/// last error) without reaching into the background-job
/// subsystem directly.
/// </summary>
public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var boardGroup = app.MapGroup("/api/boards/{boardId:guid}/webhooks")
            .RequireAuthorization()
            .WithTags("Webhooks");

        boardGroup.MapGet("/", async (
            Guid boardId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WebhookEndpointDto>>>(
                new ListWebhookEndpointsQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        boardGroup.MapPost("/", async (
            Guid boardId,
            CreateWebhookBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WebhookEndpointIssuance>>(
                new CreateWebhookEndpointCommand(
                    boardId, body.Url, body.Secret, body.Events), ct);
            return result.IsSuccess
                ? Results.Created($"/api/boards/{boardId}/webhooks/{result.Value.Endpoint.Id}", result.Value)
                : MapError(result.Error);
        });

        boardGroup.MapPatch("/{endpointId:guid}", async (
            Guid boardId,
            Guid endpointId,
            UpdateWebhookBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WebhookEndpointDto>>(
                new UpdateWebhookEndpointCommand(
                    endpointId, body.Url, body.Active), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        boardGroup.MapDelete("/{endpointId:guid}", async (
            Guid boardId,
            Guid endpointId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DeleteWebhookEndpointCommand(endpointId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        boardGroup.MapGet("/{endpointId:guid}/deliveries", async (
            Guid boardId,
            Guid endpointId,
            [FromQuery] int? take,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WebhookDeliveryDto>>>(
                new ListWebhookDeliveriesQuery(endpointId, null, 0, take ?? 50), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public sealed record CreateWebhookBody(string Url, string Secret, IReadOnlyList<string> Events);
    public sealed record UpdateWebhookBody(string? Url, IReadOnlyList<string>? Events, bool? Active);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
