using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Logging;

internal static partial class InfrastructureSettingsLogMessages
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Information, Message = "System settings updated by {UpdatedBy}: {InstanceTitle}")]
    public static partial void SettingsUpdated(ILogger logger, string updatedBy, string instanceTitle);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Warning, Message = "Failed to read system settings from {Path}, using defaults.")]
    public static partial void FailedToReadSettings(ILogger logger, string path, Exception ex);
}
