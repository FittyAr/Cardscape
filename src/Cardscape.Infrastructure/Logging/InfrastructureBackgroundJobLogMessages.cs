using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Logging;

internal static partial class InfrastructureBackgroundJobLogMessages
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} not found; skipping.")]
    internal static partial void WebhookDeliveryNotFound(this ILogger logger, Guid deliveryId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Webhook endpoint for delivery {DeliveryId} not found; dead-lettering.")]
    internal static partial void WebhookEndpointNotFound(this ILogger logger, Guid deliveryId);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} dead-lettered by SSRF re-check: {Reason}")]
    internal static partial void WebhookRejectedBySsrf(this ILogger logger, Guid deliveryId, string reason);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information, Message = "Delivered webhook {DeliveryId} ({Event}) to {Url} status {Status}.")]
    internal static partial void WebhookDelivered(this ILogger logger, Guid deliveryId, string @event, string url, int status);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} attempt {Attempt} failed (deadLetter={WillDeadLetter}).")]
    internal static partial void WebhookDeliveryAttemptFailed(this ILogger logger, Exception exception, Guid deliveryId, int attempt, bool willDeadLetter);

    [LoggerMessage(EventId = 2110, Level = LogLevel.Warning, Message = "CloneCardJob {JobId} missing or invalid cardId.")]
    internal static partial void CloneJobCardIdInvalid(this ILogger logger, Guid jobId);

    [LoggerMessage(EventId = 2111, Level = LogLevel.Warning, Message = "CloneCardJob: source card {CardId} not found.")]
    internal static partial void CloneSourceNotFound(this ILogger logger, Guid cardId);

    [LoggerMessage(EventId = 2112, Level = LogLevel.Information, Message = "CloneCardJob: source card {CardId} is archived; deferring clone.")]
    internal static partial void CloneSourceArchived(this ILogger logger, Guid cardId);

    [LoggerMessage(EventId = 2113, Level = LogLevel.Warning, Message = "CloneCardJob: source card {CardId} has no parent list; skipping.")]
    internal static partial void CloneParentListMissing(this ILogger logger, Guid cardId);

    [LoggerMessage(EventId = 2114, Level = LogLevel.Information, Message = "CloneCardJob: parent list of {CardId} is archived; deferring clone.")]
    internal static partial void CloneParentListArchived(this ILogger logger, Guid cardId);

    [LoggerMessage(EventId = 2115, Level = LogLevel.Warning, Message = "CloneCardJob: source card {CardId} has no CreatedBy; skipping clone to avoid an unattributed row.")]
    internal static partial void CloneCreatorMissing(this ILogger logger, Guid cardId);

    [LoggerMessage(EventId = 2116, Level = LogLevel.Warning, Message = "CloneCardJob: failed to create clone for {CardId}: {Code} {Msg}")]
    internal static partial void CloneCreationFailed(this ILogger logger, Guid cardId, string code, string msg);

    [LoggerMessage(EventId = 2117, Level = LogLevel.Information, Message = "Cloned card {SourceId} -> {CloneId} via recurrence.")]
    internal static partial void CardCloned(this ILogger logger, Guid sourceId, Guid cloneId);

    [LoggerMessage(EventId = 2120, Level = LogLevel.Information, Message = "CardRecurrenceDispatcherService starting: poll={Poll}")]
    internal static partial void RecurrenceDispatcherStarting(this ILogger logger, TimeSpan poll);

    [LoggerMessage(EventId = 2121, Level = LogLevel.Error, Message = "CardRecurrenceDispatcherService tick failed.")]
    internal static partial void RecurrenceDispatcherTickFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2122, Level = LogLevel.Information, Message = "CardRecurrenceDispatcherService stopping")]
    internal static partial void RecurrenceDispatcherStopping(this ILogger logger);

    [LoggerMessage(EventId = 2123, Level = LogLevel.Warning, Message = "Failed to enqueue clone for card {CardId}: {Code} {Msg}")]
    internal static partial void RecurrenceEnqueueFailed(this ILogger logger, Guid cardId, string code, string msg);

    [LoggerMessage(EventId = 2124, Level = LogLevel.Information, Message = "CardRecurrenceDispatcherService enqueued {N} jobs.")]
    internal static partial void RecurrenceJobsEnqueued(this ILogger logger, int n);
}
