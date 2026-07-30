using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

[McpServerToolType]
public sealed class ChecklistsTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "cards_list_checklists")]
    public async Task<IReadOnlyList<ChecklistDto>> ListForCard(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_list_checklists");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<ChecklistDto>>>(
                new ListCardChecklistsQuery(cardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_create_checklist")]
    public async Task<ChecklistDto> Create(Guid cardId, string title, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_create_checklist");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new CreateChecklistCommand(cardId, title), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_rename_checklist")]
    public async Task<ChecklistDto> Rename(Guid checklistId, string title, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_rename_checklist");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new RenameChecklistCommand(checklistId, title), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_delete_checklist")]
    public async Task<string> Delete(Guid checklistId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_delete_checklist");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result>(
                new DeleteChecklistCommand(checklistId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException(result.Error.Message);
            }
            __mcpSpan.MarkSuccess();
            return "deleted";
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_add_checklist_item")]
    public async Task<ChecklistDto> AddItem(Guid checklistId, string text, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_add_checklist_item");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new AddChecklistItemCommand(checklistId, text), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_rename_checklist_item")]
    public async Task<ChecklistDto> RenameItem(
        Guid checklistId, Guid itemId, string text, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_rename_checklist_item");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new RenameChecklistItemCommand(checklistId, itemId, text), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_toggle_checklist_item")]
    public async Task<ChecklistDto> ToggleItem(
        Guid checklistId, Guid itemId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_toggle_checklist_item");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new ToggleChecklistItemCommand(checklistId, itemId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_delete_checklist_item")]
    public async Task<ChecklistDto> DeleteItem(
        Guid checklistId, Guid itemId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_delete_checklist_item");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<ChecklistDto>>(
                new DeleteChecklistItemCommand(checklistId, itemId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
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
            throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }
}
