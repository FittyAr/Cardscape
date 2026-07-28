using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Api.Realtime;

/// <summary>
/// Listens to the card domain events on the Wolverine bus and
/// executes matching automation rules. Each rule is intentionally
/// limited in scope: it only triggers when the event matches the
/// configured <c>Trigger</c> (and, where applicable, the
/// configured list), and the action only writes to the same card.
/// Failed actions log a warning but never propagate the failure
/// to the originating request (automation is best-effort).
/// </summary>
public static class AutomationDispatcher
{
    public static async Task Handle(
        CardMoved @event,
        IAutomationRuleRepository rules,
        ICardRepository cards,
        IBoardListRepository lists,
        IUnitOfWork unitOfWork,
        ILogger<AutomationMarker> logger,
        CancellationToken cancellationToken)
    {
        var card = await cards.GetByIdAsync(@event.CardId, cancellationToken);
        if (card is null)
        {
            return;
        }

        var boardId = await ResolveBoardIdAsync(card, lists, cancellationToken);
        if (boardId is null)
        {
            return;
        }

        var matches = await rules.ListEnabledForBoardAsync(boardId, cancellationToken);
        foreach (var rule in matches.Where(r => r.Trigger == AutomationTrigger.CardMoved))
        {
            await ExecuteActionAsync(rule, card, cards, unitOfWork, logger, cancellationToken);
        }
    }

    public static async Task Handle(
        CardCompleted @event,
        IAutomationRuleRepository rules,
        ICardRepository cards,
        IBoardListRepository lists,
        IUnitOfWork unitOfWork,
        ILogger<AutomationMarker> logger,
        CancellationToken cancellationToken)
    {
        var card = await cards.GetByIdAsync(@event.CardId, cancellationToken);
        if (card is null)
        {
            return;
        }

        var boardId = await ResolveBoardIdAsync(card, lists, cancellationToken);
        if (boardId is null)
        {
            return;
        }

        var matches = await rules.ListEnabledForBoardAsync(boardId, cancellationToken);
        foreach (var rule in matches.Where(r => r.Trigger == AutomationTrigger.CardCompleted))
        {
            await ExecuteActionAsync(rule, card, cards, unitOfWork, logger, cancellationToken);
        }
    }

    public static async Task Handle(
        CardReopened @event,
        IAutomationRuleRepository rules,
        ICardRepository cards,
        IBoardListRepository lists,
        IUnitOfWork unitOfWork,
        ILogger<AutomationMarker> logger,
        CancellationToken cancellationToken)
    {
        var card = await cards.GetByIdAsync(@event.CardId, cancellationToken);
        if (card is null)
        {
            return;
        }

        var boardId = await ResolveBoardIdAsync(card, lists, cancellationToken);
        if (boardId is null)
        {
            return;
        }

        var matches = await rules.ListEnabledForBoardAsync(boardId, cancellationToken);
        foreach (var rule in matches.Where(r => r.Trigger == AutomationTrigger.CardReopened))
        {
            await ExecuteActionAsync(rule, card, cards, unitOfWork, logger, cancellationToken);
        }
    }

    public static async Task Handle(
        CardCreated @event,
        IAutomationRuleRepository rules,
        ICardRepository cards,
        IBoardListRepository lists,
        IUnitOfWork unitOfWork,
        ILogger<AutomationMarker> logger,
        CancellationToken cancellationToken)
    {
        if (@event.ListId is not BoardListId listId)
        {
            return;
        }

        var card = await cards.GetByIdAsync(@event.CardId, cancellationToken);
        if (card is null)
        {
            return;
        }

        var boardId = await ResolveBoardIdAsync(card, lists, cancellationToken);
        if (boardId is null)
        {
            return;
        }

        var matches = await rules.ListEnabledForBoardAsync(boardId, cancellationToken);
        foreach (var rule in matches.Where(r => r.Trigger == AutomationTrigger.CardCreatedInList
                                              && r.TriggerListId == listId.Value))
        {
            await ExecuteActionAsync(rule, card, cards, unitOfWork, logger, cancellationToken);
        }
    }

    private static async Task ExecuteActionAsync(
        BoardAutomationRule rule,
        Domain.Cards.Card card,
        ICardRepository cards,
        IUnitOfWork unitOfWork,
        ILogger<AutomationMarker> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (rule.Action)
            {
                case AutomationAction.MoveCardToList when Guid.TryParse(rule.ActionArgument, out var listId):
                    {
                        var newList = new BoardListId(listId);
                        var result = card.Move(newList, card.Position, DateTimeOffset.UtcNow);
                        if (result.IsFailure)
                        {
                            logger.LogWarning(
                                "Automation rule {RuleId} MoveCardToList failed: {Code} {Message}",
                                rule.Id, result.Error.Code, result.Error.Message);
                            return;
                        }

                        break;
                    }
                case AutomationAction.AssignUser when Guid.TryParse(rule.ActionArgument, out var userId):
                    {
                        var result = card.Assign(userId, DateTimeOffset.UtcNow);
                        if (result.IsFailure)
                        {
                            logger.LogWarning(
                                "Automation rule {RuleId} AssignUser failed: {Code} {Message}",
                                rule.Id, result.Error.Code, result.Error.Message);
                            return;
                        }

                        break;
                    }
                case AutomationAction.SetDueDate when DateTimeOffset.TryParse(
                    rule.ActionArgument, out var due):
                    {
                        var result = card.SetDueDate(due, DateTimeOffset.UtcNow);
                        if (result.IsFailure)
                        {
                            logger.LogWarning(
                                "Automation rule {RuleId} SetDueDate failed: {Code} {Message}",
                                rule.Id, result.Error.Code, result.Error.Message);
                            return;
                        }

                        break;
                    }
                case AutomationAction.MarkComplete:
                    {
                        var result = card.Complete(DateTimeOffset.UtcNow);
                        if (result.IsFailure)
                        {
                            logger.LogWarning(
                                "Automation rule {RuleId} MarkComplete failed: {Code} {Message}",
                                rule.Id, result.Error.Code, result.Error.Message);
                            return;
                        }

                        break;
                    }
                default:
                    logger.LogWarning(
                        "Automation rule {RuleId} has unknown action or missing argument: {Action} / {Arg}",
                        rule.Id, rule.Action, rule.ActionArgument);
                    return;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Automation rule {RuleId} applied {Action} to card {CardId}",
                rule.Id, rule.Action, card.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automation rule {RuleId} threw on card {CardId}", rule.Id, card.Id);
        }
    }

    private static async Task<Domain.Boards.BoardId?> ResolveBoardIdAsync(
        Domain.Cards.Card card,
        IBoardListRepository lists,
        CancellationToken cancellationToken)
    {
        var map = await lists.ListBoardIdsByListIdAsync(cancellationToken);
        return map.TryGetValue(card.ListId.Value, out var boardId) ? new Domain.Boards.BoardId(boardId) : null;
    }
}

/// <summary>Marker type so we can inject <see cref="ILogger{T}"/> generically.</summary>
public sealed class AutomationMarker;
