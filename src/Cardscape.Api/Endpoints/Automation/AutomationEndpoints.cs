using Cardscape.Application.Automation;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Automation;

/// <summary>
/// REST surface for board automation rules. Rules are scoped to a
/// board; any member of the board can list them; only the board
/// owner can currently mutate them (v0.6.3).
/// </summary>
public static class AutomationEndpoints
{
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/automation")
            .RequireAuthorization()
            .WithTags("Automation");

        group.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardAutomationRuleDto>>>(
                new ListBoardAutomationRulesQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async (
            Guid boardId,
            CreateRuleBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardAutomationRuleDto>>(
                new CreateBoardAutomationRuleCommand(
                    boardId, body.Name, (AutomationTrigger)body.Trigger, body.TriggerListId,
                    (AutomationAction)body.Action, body.ActionArgument, body.Position),
                ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/boards/{boardId}/automation/{result.Value.Id}",
                    result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/{ruleId:guid}/enable", async (Guid boardId, Guid ruleId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new EnableBoardAutomationRuleCommand(ruleId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapPost("/{ruleId:guid}/disable", async (Guid boardId, Guid ruleId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DisableBoardAutomationRuleCommand(ruleId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapDelete("/{ruleId:guid}", async (Guid boardId, Guid ruleId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DeleteBoardAutomationRuleCommand(ruleId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record CreateRuleBody(
        string Name,
        AutomationTrigger Trigger,
        Guid? TriggerListId,
        AutomationAction Action,
        string? ActionArgument,
        int Position = 0);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
