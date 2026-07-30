using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Extensions;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for board extensions. Kinds: 0 = CustomFields,
/// 1 = Voting, 2 = CardRepeater. <c>configJson</c> is opaque to
/// Cardscape core; pass the JSON shape documented by the matching
/// extension feature.
/// </summary>
[McpServerToolType]
public sealed class BoardExtensionsTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "boards_list_extensions")]
    public async Task<IReadOnlyList<BoardExtensionDto>> ListExtensions(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_list_extensions");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardExtensionDto>>>(
            new ListBoardExtensionsQuery(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "boards_enable_extension")]
    public async Task<BoardExtensionDto> EnableExtension(
        Guid boardId,
        int kind,
        string? configJson,
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_enable_extension");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardExtensionDto>>(
            new EnableBoardExtensionCommand(boardId, kind, configJson), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "boards_disable_extension")]
    public async Task<string> DisableExtension(Guid boardId, int kind, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_disable_extension");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(
            new DisableBoardExtensionCommand(boardId, kind), ct);
        Ensure(result);
        return "disabled";
    }

    [McpServerTool(Name = "boards_update_extension_config")]
    public async Task<BoardExtensionDto> UpdateExtensionConfig(
        Guid boardId,
        int kind,
        string? configJson,
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_update_extension_config");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardExtensionDto>>(
            new UpdateBoardExtensionConfigCommand(boardId, kind, configJson), ct);
        return Ensure(result);
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

