using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Queries;
using Cardscape.Application.Lists.Commands;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Lists.Queries;
using Cardscape.Application.Realtime;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

public sealed partial class BoardsTools
{
    [McpServerTool(Name = "workspaces_list")]
    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspaces(CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("workspaces_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceDto>>>(new ListWorkspacesForUserQuery(), ct);
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

    [McpServerTool(Name = "boards_list")]
    public async Task<IReadOnlyList<BoardSummaryDto>> ListBoards(Guid workspaceId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardSummaryDto>>>(
                new ListBoardsForWorkspaceQuery(workspaceId), ct);
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

    [McpServerTool(Name = "boards_get")]
    public async Task<BoardDto> GetBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_get");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(new GetBoardQuery(boardId), ct);
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

    [McpServerTool(Name = "boards_create")]
    public async Task<BoardDto> CreateBoard(
        Guid workspaceId, string name, string? description, int visibility, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(
                new CreateBoardCommand(workspaceId, name, description, (Cardscape.Domain.Boards.BoardVisibility)visibility),
                ct);
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

    [McpServerTool(Name = "boards_star")]
    public async Task<BoardDto> StarBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_star");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(new StarBoardCommand(boardId), ct);
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

    [McpServerTool(Name = "boards_unstar")]
    public async Task<BoardDto> UnstarBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_unstar");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(new UnstarBoardCommand(boardId), ct);
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

    [McpServerTool(Name = "lists_list")]
    public async Task<IReadOnlyList<BoardListDto>> ListLists(
        Guid boardId, bool includeArchived, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("lists_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardListDto>>>(
                new ListListsForBoardQuery(boardId, includeArchived), ct);
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

    [McpServerTool(Name = "lists_create")]
    public async Task<BoardListDto> CreateList(
        Guid boardId,
        string name,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("lists_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardListDto>>(
                new CreateListCommand(boardId, name), ct);
            var value = Ensure(result);
            await push.PushListCreatedAsync(new ListEventPayload(
                value.Id, value.BoardId, value.Name, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }
}

