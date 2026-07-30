using System.Diagnostics;

namespace Cardscape.Mcp.Observability;

/// <summary>
/// Helpers for wrapping an MCP tool call in an OpenTelemetry
/// span named <c>mcp.tool.&lt;name&gt;</c>. Used by every
/// <c>[McpServerTool]</c> method so the call graph is
/// reconstructable end-to-end in the OTLP backend.
/// </summary>
public static class McpToolSpan
{
    /// <summary>
    /// Begins a tool span. The returned
    /// <see cref="McpToolSpanScope"/> is the canonical child
    /// of the current ambient activity (or a root span when
    /// none is active). The caller is expected to dispose the
    /// returned object; <see cref="McpToolSpanScope.Dispose"/>
    /// stops the span.
    /// </summary>
    /// <param name="toolName">Logical name of the MCP tool,
    /// e.g. <c>boards_list</c>. Becomes the span name
    /// <c>mcp.tool.&lt;toolName&gt;</c>.</param>
    public static McpToolSpanScope Begin(string toolName)
    {
        string spanName = $"mcp.tool.{toolName}";
        Activity? activity = McpTracing.ActivitySource.StartActivity(
            spanName,
            ActivityKind.Internal);

        return new McpToolSpanScope(activity, toolName);
    }
}

/// <summary>
/// <see cref="IDisposable"/> handle returned by
/// <see cref="McpToolSpan.Begin"/>. Stops the underlying
/// <see cref="Activity"/> on dispose and records the
/// <c>mcp.tool.outcome</c> tag (set via
/// <see cref="MarkSuccess"/> / <see cref="MarkFailure"/>).
/// </summary>
public readonly struct McpToolSpanScope : IDisposable
{
    private readonly Activity? _activity;

    public McpToolSpanScope(Activity? activity, string toolName)
    {
        _activity = activity;
        _activity?.SetTag("mcp.tool.name", toolName);
    }

    /// <summary>
    /// Records per-invocation context on the span:
    /// <c>user.id</c>, <c>board.id</c>, and/or
    /// <c>card.id</c> when the caller has them. Null/empty
    /// inputs are skipped so the span only carries the
    /// attributes that apply to the current tool.
    /// </summary>
    public void SetContext(string? userId, Guid? boardId, Guid? cardId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            _activity?.SetTag("user.id", userId);
        }
        if (boardId is { } b && b != Guid.Empty)
        {
            _activity?.SetTag("board.id", b.ToString());
        }
        if (cardId is { } c && c != Guid.Empty)
        {
            _activity?.SetTag("card.id", c.ToString());
        }
    }

    public void MarkSuccess() => _activity?.SetTag("mcp.tool.outcome", "success");

    /// <summary>
    /// Marks the span as a failure. Sets
    /// <c>mcp.tool.outcome = failure</c>, plus
    /// <c>mcp.tool.error.code</c> and
    /// <c>mcp.tool.error.message</c> when provided, and
    /// flips the activity status to <see cref="ActivityStatusCode.Error"/>
    /// so the OTel backend renders the span as failed.
    /// </summary>
    public void MarkFailure(string? code = null, string? message = null)
    {
        _activity?.SetTag("mcp.tool.outcome", "failure");
        if (!string.IsNullOrWhiteSpace(code))
        {
            _activity?.SetTag("mcp.tool.error.code", code);
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            _activity?.SetTag("mcp.tool.error.message", message);
        }
        _activity?.SetStatus(ActivityStatusCode.Error, message ?? code);
    }

    public void SetTag(string key, object? value) =>
        _activity?.SetTag(key, value);

    public void Dispose() => _activity?.Dispose();
}
