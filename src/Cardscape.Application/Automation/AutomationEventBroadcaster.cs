using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Cards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Automation;

/// <summary>
/// Automation rule fan-out for card events. Every board can
/// declare rules that listen for one of the four supported
/// triggers (CardMoved, CardCompleted, CardReopened,
/// CardCreatedInList) and run a matching action
/// (MoveCardToList, AssignUser, SetDueDate, MarkComplete).
/// The broadcaster runs the matching rules for every raised
/// event; the action is best-effort — a failure logs a
/// warning but never propagates to the originating request
/// (automation is side-channel, not a precondition).
/// <para>
/// The switch on runtime type is the dispatch
/// strategy: <see cref="IDomainEvent"/> values do not
/// implement <c>Wolverine.IMessage</c>, so Wolverine's
/// static-handler discovery does not pick them up. The
/// <c>WolverineDomainEventDispatcher</c> invokes
/// <see cref="BroadcastAsync"/> directly and the
/// <c>switch</c> lives here.
/// </para>
/// <para>
/// The broadcaster is registered as a singleton; the EF
/// Core repositories it depends on are scoped, so the
/// broadcaster creates a fresh <see cref="IServiceScope"/>
/// per event (the scope is disposed when the handler
/// returns). The work is awaited inline so the
/// <c>WolverineDomainEventDispatcher.DispatchAsync</c>
/// pipeline can complete before the scope is disposed.
/// </para>
/// </summary>
public sealed class AutomationEventBroadcaster : IDomainEventBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationEventBroadcaster> _logger;

    public AutomationEventBroadcaster(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationEventBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task BroadcastAsync(IDomainEvent @event, CancellationToken ct = default) =>
        @event switch
        {
            CardCreated e => HandleCardCreated(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => HandleCardCompleted(e, ct),
            CardReopened e => HandleCardReopened(e, ct),
            _ => Task.CompletedTask
        };

    private async Task HandleCardCreated(CardCreated @event, CancellationToken ct)
    {
        if (@event.ListId is not BoardListId listId)
        {
            return;
        }

        await RunForCardAsync(
            listId: listId,
            cardId: @event.CardId,
            trigger: AutomationTrigger.CardCreatedInList,
            extraFilter: rule => rule.TriggerListId == listId.Value,
            ct: ct);
    }

    private Task HandleCardMoved(CardMoved @event, CancellationToken ct) =>
        RunForCardAsync(
            listId: @event.NewListId,
            cardId: @event.CardId,
            trigger: AutomationTrigger.CardMoved,
            extraFilter: _ => true,
            ct: ct);

    private Task HandleCardCompleted(CardCompleted @event, CancellationToken ct) =>
        RunForCardAsync(
            listId: null,
            cardId: @event.CardId,
            trigger: AutomationTrigger.CardCompleted,
            extraFilter: _ => true,
            ct: ct,
            resolveListIdFromCard: true);

    private Task HandleCardReopened(CardReopened @event, CancellationToken ct) =>
        RunForCardAsync(
            listId: null,
            cardId: @event.CardId,
            trigger: AutomationTrigger.CardReopened,
            extraFilter: _ => true,
            ct: ct,
            resolveListIdFromCard: true);

    private async Task RunForCardAsync(
        BoardListId? listId,
        CardId cardId,
        AutomationTrigger trigger,
        Func<BoardAutomationRule, bool> extraFilter,
        CancellationToken ct,
        bool resolveListIdFromCard = false)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ICardRepository cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
            IBoardListRepository lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
            IAutomationRuleRepository rules = scope.ServiceProvider.GetRequiredService<IAutomationRuleRepository>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Card? card = await cards.GetByIdAsync(cardId, ct);
            if (card is null)
            {
                return;
            }

            BoardListId effectiveListId = listId ?? (resolveListIdFromCard ? card.ListId : card.ListId);
            BoardList? list = await lists.GetByIdAsync(effectiveListId, ct);
            if (list is null)
            {
                return;
            }

            IReadOnlyList<BoardAutomationRule> matches = await rules.ListEnabledForBoardAsync(list.BoardId, ct);
            matches = matches.Where(r => r.Trigger == trigger && extraFilter(r)).ToList();
            if (matches.Count == 0)
            {
                return;
            }

            foreach (BoardAutomationRule rule in matches)
            {
                await ExecuteActionAsync(rule, card, unitOfWork, _logger, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AutomationEventBroadcaster failed while processing {Trigger} for card {CardId}",
                trigger, cardId);
        }
    }

    private static async Task ExecuteActionAsync(
        BoardAutomationRule rule,
        Card card,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            switch (rule.Action)
            {
                case AutomationAction.MoveCardToList when Guid.TryParse(rule.ActionArgument, out var listId):
                    {
                        var result = card.Move(new BoardListId(listId), card.Position, DateTimeOffset.UtcNow);
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

            await unitOfWork.SaveChangesAsync(ct);
            logger.LogInformation(
                "Automation rule {RuleId} applied {Action} to card {CardId}",
                rule.Id, rule.Action, card.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automation rule {RuleId} threw on card {CardId}", rule.Id, card.Id);
        }
    }
}
