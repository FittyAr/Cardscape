using Cardscape.Application.Comments.Commands;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Application.Comments.Queries;
using Cardscape.Application.Labels.Commands;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Application.Labels.Queries;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

public sealed partial class BoardsTools
{
    [McpServerTool(Name = "comments_add")]
    public async Task<CommentDto> AddComment(Guid cardId, string body, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_add");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CommentDto>>(
                new AddCommentCommand(cardId, body), ct);
            var dto = Ensure(result);
            await push.PushCommentAddedAsync(new CommentEventPayload(
                dto.Id, dto.CardId, Guid.Empty, dto.AuthorId, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "comments_list")]
    public async Task<IReadOnlyList<CommentDto>> ListComments(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CommentDto>>>(
                new ListCommentsForCardQuery(cardId), ct);
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

    // BETA-8-MCP-#2 — see test-results/r8/r8-report.md.
    // comments_add existed but edit/delete were not surfaced as
    // MCP tools, so an AI client could post a comment and then
    // had no way to fix a typo or remove it. Both tools delegate
    // to the existing EditCommentCommand / DeleteCommentCommand
    // handlers so the IDOR fix, search-index re-index, and
    // activity-feed write all stay in one place.
    [McpServerTool(Name = "comments_edit")]
    public async Task<CommentDto> EditComment(Guid commentId, string newBody, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_edit");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CommentDto>>(
                new EditCommentCommand(commentId, newBody), ct);
            var dto = Ensure(result);
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "comments_delete")]
    public async Task<object> DeleteComment(Guid commentId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_delete");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result>(
                new DeleteCommentCommand(commentId), ct);
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
            __mcpSpan.MarkSuccess();
            return new { ok = true, commentId };
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "labels_list")]
    public async Task<IReadOnlyList<LabelDto>> ListLabels(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("labels_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<LabelDto>>>(
                new ListLabelsForBoardQuery(boardId), ct);
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

    [McpServerTool(Name = "labels_create")]
    public async Task<LabelDto> CreateLabel(Guid boardId, string name, string color, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("labels_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<LabelDto>>(
                new CreateLabelCommand(boardId, name, color), ct);
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
}

