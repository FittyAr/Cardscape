using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Logging;

internal static partial class InfrastructureHostedLogMessages
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Error, Message = "RetentionSweeper iteration failed; the next tick will retry")]
    internal static partial void RetentionIterationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "RetentionSweeper: anonymised {Count} users past the grace period")]
    internal static partial void UsersAnonymised(this ILogger logger, int count);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "RetentionSweeper: purged {Count} activity feed entries older than {Days} days")]
    internal static partial void ActivityEntriesPurged(this ILogger logger, int count, int days);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "RetentionSweeper: purged {Count} expired idempotency keys")]
    internal static partial void IdempotencyKeysPurged(this ILogger logger, int count);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Information, Message = "RevocationSweeper is disabled (RevocationSweeper:Enabled=false). Expired rows will not be purged automatically.")]
    internal static partial void RevocationSweeperDisabled(this ILogger logger);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Information, Message = "RevocationSweeper purged {Count} expired revoked-token row(s).")]
    internal static partial void RevokedTokensPurged(this ILogger logger, int count);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Error, Message = "RevocationSweeper failed; will retry after the next interval.")]
    internal static partial void RevocationSweepFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Error, Message = "Domain event outbox dispatch cycle failed")]
    internal static partial void OutboxDispatchCycleFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Warning, Message = "Domain event outbox delivery {DeliveryId} failed on attempt {Attempt}")]
    internal static partial void OutboxDeliveryFailed(this ILogger logger, Exception exception, Guid deliveryId, int attempt);

    [LoggerMessage(EventId = 2022, Level = LogLevel.Error, Message = "Immediate domain event outbox dispatch failed; durable deliveries remain pending")]
    internal static partial void ImmediateOutboxDispatchFailed(this ILogger logger, Exception exception);
}
