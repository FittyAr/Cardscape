using Cardscape.Api.Filters;
using Cardscape.Application.Integrations.Slack.Commands;
using Cardscape.Application.Integrations.Slack.DTOs;
using Cardscape.Application.Integrations.Slack.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Integrations;

/// <summary>
/// REST endpoints for the Slack integration. Mounted under
/// <c>/api/workspaces/{id}/integrations/slack</c> so OAuth connect
/// and channel management share the same workspace-scoped prefix.
/// </summary>
public static class SlackEndpoints
{
    public static IEndpointRouteBuilder MapSlackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/workspaces/{workspaceId:guid}/integrations/slack")
            .RequireAuthorization()
            .RequireRegionGuard()
            .WithTags("Integrations.Slack");

        group.MapGet("/", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<SlackWorkspaceDto?>>(
                new GetSlackWorkspaceForWorkspaceQuery(workspaceId), ct);
            // BETA-A2-004 — see test-results/beta/00-FINAL-SUMMARY.md.
            // When the user has not connected Slack, the
            // handler returns `SlackWorkspaceDto?` null. The
            // previous `Results.Ok(result.Value)` serialised a
            // 0-byte body which made the WASM client's
            // `ReadFromJsonAsync` throw `JsonException`. Bounce
            // null to `Results.NoContent()` so the client sees
            // the canonical "no connection" path (the existing
            // `ApiClientBase.ReadAsync` already handles a 204
            // and returns Ok(default) for nullable T).
            return result.IsSuccess
                ? result.Value is null
                    ? Results.NoContent()
                    : Results.Ok(result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        })
            .Produces<SlackWorkspaceDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/connect", async (Guid workspaceId, [FromBody] ConnectSlackRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<SlackWorkspaceDto>>(
                new ConnectSlackWorkspaceCommand(
                    workspaceId, body.TeamId, body.TeamName, body.BotToken),
                ct);
            return result.IsSuccess
                ? Results.Created($"/api/workspaces/{workspaceId}/integrations/slack", result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces<SlackWorkspaceDto>(StatusCodes.Status201Created);

        group.MapGet("/channels", async (Guid workspaceId, [FromQuery] Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<SlackChannelDto>>>(
                new ListSlackChannelsForBoardQuery(workspaceId, boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        }).Produces<IReadOnlyList<SlackChannelDto>>(StatusCodes.Status200OK);

        group.MapPost("/channels", async (Guid workspaceId, [FromBody] LinkSlackChannelRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<SlackChannelDto>>(
                new LinkSlackChannelCommand(
                    workspaceId, body.SlackWorkspaceId, body.BoardId,
                    body.ChannelId, body.ChannelName, body.Events),
                ct);
            return result.IsSuccess
                ? Results.Created($"/api/workspaces/{workspaceId}/integrations/slack/channels/{result.Value.Id}", result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces<SlackChannelDto>(StatusCodes.Status201Created);

        group.MapDelete("/channels/{channelId:guid}", async (Guid workspaceId, Guid channelId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new UnlinkSlackChannelCommand(workspaceId, channelId), ct);
            return result.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        return app;
    }

    public sealed record ConnectSlackRequest(string TeamId, string TeamName, string BotToken);
    public sealed record LinkSlackChannelRequest(
        Guid SlackWorkspaceId, Guid BoardId, string ChannelId, string ChannelName, IReadOnlyList<string> Events);

}
