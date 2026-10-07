using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Automation;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for board automation rules. AI assistants can
/// list rules on a board, create new ones, and toggle them on/off.
/// Triggers: 0 = CardMoved, 1 = CardCompleted, 2 = CardReopened,
/// 3 = CardCreatedInList. Actions: 0 = MoveCardToList, 1 = AssignUser,
/// 2 = SetDueDate, 3 = MarkComplete. <c>actionArgument</c> is the
/// list id (Move), user id (Assign), or ISO-8601 timestamp (SetDueDate).
/// The numeric codes apply to tool arguments only; returned rules type
/// <c>trigger</c> and <c>action</c> as enums (ADR 0014).
/// </summary>
[McpServerToolType]
public sealed class AutomationTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "automation_list_rules")]
    public async Task<IReadOnlyList<BoardAutomationRuleDto>> ListRulesAsync(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_list_rules");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardAutomationRuleDto>>>(
                new ListBoardAutomationRulesQuery(boardId), ct);
            var value = result.OrThrow();
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "automation_create_rule")]
    public async Task<BoardAutomationRuleDto> CreateRuleAsync(
        Guid boardId,
        string name,
        int trigger,
        Guid? triggerListId,
        int action,
        string? actionArgument,
        int position,
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_create_rule");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result<BoardAutomationRuleDto>>(
                new CreateBoardAutomationRuleCommand(
                    boardId, name, (Domain.Boards.AutomationTrigger)trigger, triggerListId,
                    (Domain.Boards.AutomationAction)action, actionArgument, position),
                ct);
            var value = result.OrThrow();
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "automation_enable_rule")]
    public async Task<string> EnableRuleAsync(Guid ruleId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_enable_rule");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result>(new EnableBoardAutomationRuleCommand(ruleId), ct);
            result.OrThrow();
            __mcpSpan.MarkSuccess();
            return "enabled";
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "automation_disable_rule")]
    public async Task<string> DisableRuleAsync(Guid ruleId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_disable_rule");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result>(new DisableBoardAutomationRuleCommand(ruleId), ct);
            result.OrThrow();
            __mcpSpan.MarkSuccess();
            return "disabled";
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "automation_delete_rule")]
    public async Task<string> DeleteRuleAsync(Guid ruleId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_delete_rule");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result>(new DeleteBoardAutomationRuleCommand(ruleId), ct);
            result.OrThrow();
            __mcpSpan.MarkSuccess();
            return "deleted";
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
