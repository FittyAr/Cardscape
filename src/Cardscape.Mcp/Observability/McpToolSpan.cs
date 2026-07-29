using System.Diagnostics;
using Cardscape.Mcp.Observability;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// Helpers for wrapping an MCP tool call in an OpenTelemetry
/// span named <c>mcp.tool.&lt;name&gt;</c>. Used by every
/// <c>[McpServerTool]</c> method so the call graph is
/// reconstructable end-to-end in the OTLP backend.
/// </summary>
public static class McpToolSpan
{
    /// <summary>
    /// Begins a tool span. The returned <see cref="Activity"/>
    /// is the canonical child of the current ambient activity
    /// (or a root span when none is active). The caller is
    /// expected to dispose the returned object; the
    /// <see cref="McpToolSpanScope.Dispose"/> method stops
    /// the span and records the outcome tag.
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

    public void MarkSuccess() => _activity?.SetTag("mcp.tool.outcome", "ok");
    public void MarkFailure(string? reason = null)
    {
        _activity?.SetTag("mcp.tool.outcome", "error");
        if (!string.IsNullOrWhiteSpace(reason))
        {
            _activity?.SetTag("mcp.tool.error", reason);
        }
        _activity?.SetStatus(ActivityStatusCode.Error, reason);
    }

    public void SetTag(string key, object? value) =>
        _activity?.SetTag(key, value);

    public void Dispose() => _activity?.Dispose();
}
