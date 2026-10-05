using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Logging;

internal static partial class InfrastructureSettingsLogMessages
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Information, Message = "System settings updated by {UpdatedBy}: {InstanceTitle}")]
    public static partial void SettingsUpdated(ILogger logger, string updatedBy, string instanceTitle);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Warning, Message = "Failed to read system settings from {Path}, using defaults.")]
    public static partial void FailedToReadSettings(ILogger logger, string path, Exception ex);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Information, Message = "System settings reset to defaults by {ResetBy}")]
    public static partial void SettingsReset(ILogger logger, string resetBy);

    [LoggerMessage(EventId = 2303, Level = LogLevel.Information, Message = "AI connection test executed. Success: {Success}")]
    public static partial void AiTestExecuted(ILogger logger, bool success);

    [LoggerMessage(EventId = 2304, Level = LogLevel.Information, Message = "Email test executed to {TargetEmail}. Success: {Success}")]
    public static partial void EmailTestExecuted(ILogger logger, string targetEmail, bool success);
}
