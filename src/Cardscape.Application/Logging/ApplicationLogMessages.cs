using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Logging;

internal static partial class ApplicationLogMessages
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "AutomationEventBroadcaster failed while processing {Trigger} for card {CardId}")]
    internal static partial void AutomationBroadcastFailed(this ILogger logger, Exception exception, AutomationTrigger trigger, CardId cardId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Automation rule {RuleId} MoveCardToList failed: {Code} {Message}")]
    internal static partial void AutomationMoveFailed(this ILogger logger, BoardAutomationRuleId ruleId, string code, string message);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Automation rule {RuleId} AssignUser failed: {Code} {Message}")]
    internal static partial void AutomationAssignmentFailed(this ILogger logger, BoardAutomationRuleId ruleId, string code, string message);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Automation rule {RuleId} SetDueDate failed: {Code} {Message}")]
    internal static partial void AutomationDueDateFailed(this ILogger logger, BoardAutomationRuleId ruleId, string code, string message);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Automation rule {RuleId} MarkComplete failed: {Code} {Message}")]
    internal static partial void AutomationCompletionFailed(this ILogger logger, BoardAutomationRuleId ruleId, string code, string message);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Automation rule {RuleId} has unknown action or missing argument: {Action} / {Arg}")]
    internal static partial void AutomationActionInvalid(this ILogger logger, BoardAutomationRuleId ruleId, AutomationAction action, string? arg);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Automation rule {RuleId} applied {Action} to card {CardId}")]
    internal static partial void AutomationApplied(this ILogger logger, BoardAutomationRuleId ruleId, AutomationAction action, CardId cardId);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Error, Message = "Automation rule {RuleId} threw on card {CardId}")]
    internal static partial void AutomationActionThrew(this ILogger logger, Exception exception, BoardAutomationRuleId ruleId, CardId cardId);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "BoardEventBroadcaster.CardCreated for {CardId}")]
    internal static partial void CardCreatedBroadcast(this ILogger logger, CardId cardId);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Warning, Message = "Google Calendar push for card {CardId} user {UserId} failed: {Code} {Message}")]
    internal static partial void CalendarPushFailed(this ILogger logger, Guid cardId, Guid userId, string code, string message);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Error, Message = "Google Calendar push for card {CardId} user {UserId} threw.")]
    internal static partial void CalendarPushThrew(this ILogger logger, Exception exception, Guid cardId, Guid userId);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Debug, Message = "Queued {Count} webhook delivery job(s) for {EventType} on board {BoardId}")]
    internal static partial void WebhookDeliveriesQueued(this ILogger logger, int count, string eventType, BoardId boardId);
}
