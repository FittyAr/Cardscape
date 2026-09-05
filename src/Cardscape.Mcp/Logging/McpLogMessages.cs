using Microsoft.Extensions.Logging;

namespace Cardscape.Mcp.Logging;

internal static partial class McpLogMessages
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Rejected MCP API token: {ErrorCode}")]
    internal static partial void ApiTokenRejected(this ILogger logger, string errorCode);

    [LoggerMessage(EventId = 5010, Level = LogLevel.Warning, Message = "MCP ResourceUpdated notification for {Uri} failed for one session; dropping that subscriber")]
    internal static partial void ResourceNotificationFailed(this ILogger logger, Exception exception, string uri);

    [LoggerMessage(EventId = 5011, Level = LogLevel.Debug, Message = "MCP ResourceUpdated notification for {Uri} sent to {Sent}/{Total} subscribers")]
    internal static partial void ResourceNotificationSent(this ILogger logger, string uri, int sent, int total);

    [LoggerMessage(EventId = 5020, Level = LogLevel.Warning, Message = "Cardscape:Internal:Secret is not set on the MCP server. Realtime broadcasts from the MCP will be rejected by the API.")]
    internal static partial void InternalSecretMissing(this ILogger logger);

    [LoggerMessage(EventId = 5021, Level = LogLevel.Warning, Message = "MCP->API broadcast {Method} failed: {Status}")]
    internal static partial void ApiBroadcastUnsuccessful(this ILogger logger, string method, int status);

    [LoggerMessage(EventId = 5022, Level = LogLevel.Warning, Message = "MCP->API broadcast {Method} threw")]
    internal static partial void ApiBroadcastFailed(this ILogger logger, Exception exception, string method);
}
