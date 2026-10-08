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

    [LoggerMessage(EventId = 2305, Level = LogLevel.Information, Message = "Migrated legacy system settings file {Path} to schema version {Version}")]
    public static partial void LegacySettingsMigrated(ILogger logger, string path, int version);

    [LoggerMessage(EventId = 2306, Level = LogLevel.Warning, Message = "Stored AI API key could not be decrypted (data-protection keys changed?); it must be entered again.")]
    public static partial void AiApiKeyUnreadable(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 2307, Level = LogLevel.Warning, Message = "Stored SMTP password could not be decrypted (data-protection keys changed?); it must be entered again.")]
    public static partial void SmtpPasswordUnreadable(ILogger logger, Exception ex);
}

internal static partial class InfrastructureEmailLogMessages
{
    [LoggerMessage(EventId = 2400, Level = LogLevel.Information, Message = "Email \"{Subject}\" sent to a {RecipientDomain} address through {Host}:{Port}")]
    public static partial void EmailSent(ILogger logger, string subject, string recipientDomain, string host, int port);

    [LoggerMessage(EventId = 2401, Level = LogLevel.Warning, Message = "Email \"{Subject}\" to a {RecipientDomain} address could not be sent through {Host}:{Port}")]
    public static partial void EmailFailed(ILogger logger, string subject, string recipientDomain, string host, int port, Exception ex);
}
