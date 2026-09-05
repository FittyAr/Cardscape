using Microsoft.Extensions.Logging;

namespace Cardscape.Seeder.Logging;

internal static partial class SeederLogMessages
{
    [LoggerMessage(EventId = 6000, Level = LogLevel.Error, Message = "Seed step {Step} failed")]
    internal static partial void SeedStepFailed(this ILogger logger, Exception exception, string step);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Error, Message = "Seed run failed")]
    internal static partial void SeedRunFailed(this ILogger logger, Exception exception);
}
