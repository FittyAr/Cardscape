using Cardscape.Application.Abstractions.Calendar;
using Cardscape.Application.Calendar;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

public sealed partial class BoardsTools
{
    [McpServerTool(Name = "boards_get_icalendar")]
    public async Task<string> GetBoardICalendar(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_get_icalendar");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<Stream>>(
                new RenderBoardCalendarQuery(boardId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
            using var reader = new StreamReader(result.Value);
            var value = await reader.ReadToEndAsync(ct);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "boards_export")]
    public async Task<byte[]> ExportBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_export");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<Stream>>(
                new ExportBoardQuery(boardId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
            using var ms = new MemoryStream();
            await result.Value.CopyToAsync(ms, ct);
            var value = ms.ToArray();
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
