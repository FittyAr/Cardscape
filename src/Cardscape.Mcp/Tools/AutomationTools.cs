using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Automation;
using Cardscape.Domain.Common;
using ModelContextProtocol.Server;
using Cardscape.Mcp.Observability;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for board automation rules. AI assistants can
/// list rules on a board, create new ones, and toggle them on/off.
/// Triggers: 0 = CardMoved, 1 = CardCompleted, 2 = CardReopened,
/// 3 = CardCreatedInList. Actions: 0 = MoveCardToList, 1 = AssignUser,
/// 2 = SetDueDate, 3 = MarkComplete. <c>actionArgument</c> is the
/// list id (Move), user id (Assign), or ISO-8601 timestamp (SetDueDate).
/// </summary>
[McpServerToolType]
public sealed class AutomationTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "automation_list_rules")]
    public async Task<IReadOnlyList<BoardAutomationRuleDto>> ListRules(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_list_rules");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardAutomationRuleDto>>>(
            new ListBoardAutomationRulesQuery(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "automation_create_rule")]
    public async Task<BoardAutomationRuleDto> CreateRule(
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
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardAutomationRuleDto>>(
            new CreateBoardAutomationRuleCommand(
                boardId, name, (Domain.Boards.AutomationTrigger)trigger, triggerListId,
                (Domain.Boards.AutomationAction)action, actionArgument, position),
            ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "automation_enable_rule")]
    public async Task<string> EnableRule(Guid ruleId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_enable_rule");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(new EnableBoardAutomationRuleCommand(ruleId), ct);
        Ensure(result);
        return "enabled";
    }

    [McpServerTool(Name = "automation_disable_rule")]
    public async Task<string> DisableRule(Guid ruleId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_disable_rule");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(new DisableBoardAutomationRuleCommand(ruleId), ct);
        Ensure(result);
        return "disabled";
    }

    [McpServerTool(Name = "automation_delete_rule")]
    public async Task<string> DeleteRule(Guid ruleId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("automation_delete_rule");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(new DeleteBoardAutomationRuleCommand(ruleId), ct);
        Ensure(result);
        return "deleted";
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT or API token in the Authorization header.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }
    }
}

