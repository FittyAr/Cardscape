using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
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
/// event. Delivery failures propagate to the durable outbox, which retries
/// this broadcaster without rolling back the originating request.
/// <para>
/// The switch on runtime type is the dispatch
/// strategy: <see cref="IDomainEvent"/> values do not
/// implement <c>Wolverine.IMessage</c>, so Wolverine's
/// static-handler discovery does not pick them up. The
/// The durable domain-event outbox invokes <see cref="BroadcastAsync"/> and the
/// <c>switch</c> lives here.
/// </para>
/// <para>
/// The broadcaster is registered as a singleton; the EF
/// Core repositories it depends on are scoped, so the
/// broadcaster creates a fresh <see cref="IServiceScope"/>
/// per event (the scope is disposed when the handler
/// returns). The work is awaited inline so the
/// outbox delivery can complete before the scope is disposed.
/// </para>
/// </summary>
public sealed class AutomationEventBroadcaster : IDomainEventBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<AutomationEventBroadcaster> _logger;

    public AutomationEventBroadcaster(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<AutomationEventBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    public Task BroadcastAsync(IDomainEvent domainEvent, CancellationToken ct = default) =>
        domainEvent switch
        {
            // BETA-A7-R2-001 — see test-results/beta/round-2/reports/A7-advanced.md.
            // The automation broadcaster must ignore events
            // that ITS OWN action raised. Otherwise a rule
            // like `CardMoved + MoveCardToList(B)` fires,
            // the action mutates the card to list B, that
            // mutation re-emits `CardMoved`, the rule fires
            // again, and the system enters a chain reaction
            // (verified: 4 fires per single user move).
            // We track the "we are already inside an
            // automation broadcast" flag via AsyncLocal so
            // the self-trigger is dropped without having to
            // thread an actor id through every Card method.
            var e when InAutomationBroadcast => Task.CompletedTask,
            CardCreated e => HandleCardCreated(e, ct),
            CardMoved e => HandleCardMoved(e, ct),
            CardCompleted e => HandleCardCompleted(e, ct),
            CardReopened e => HandleCardReopened(e, ct),
            _ => Task.CompletedTask
        };

    /// <summary>
    /// BETA-A7-R2-001 — see test-results/beta/round-2/reports/A7-advanced.md.
    /// AsyncLocal flag the broadcaster flips on while it
    /// is processing an event. The Card mutations the
    /// action runs (Move / SetDueDate / Complete / Assign)
    /// go through the SaveChanges interceptor and dispatch
    /// a new event; that new event hits BroadcastAsync
    /// again, sees the flag is set, and returns. The flag
    /// is reset by the `using` block below, so the
    /// re-entry is contained to the synchronous call
    /// tree of the original user action.
    /// </summary>
    private static readonly AsyncLocal<bool> _inAutomationBroadcast = new();

    private static bool InAutomationBroadcast => _inAutomationBroadcast.Value;

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
            ct: ct);

    private Task HandleCardReopened(CardReopened @event, CancellationToken ct) =>
        RunForCardAsync(
            listId: null,
            cardId: @event.CardId,
            trigger: AutomationTrigger.CardReopened,
            extraFilter: _ => true,
            ct: ct);

    private async Task RunForCardAsync(
        BoardListId? listId,
        CardId cardId,
        AutomationTrigger trigger,
        Func<BoardAutomationRule, bool> extraFilter,
        CancellationToken ct)
    {
        // BETA-A7-R2-001 — see test-results/beta/round-2/reports/A7-advanced.md.
        // Set the AsyncLocal flag so the broadcaster drops
        // the events the action is about to raise. The flag
        // is reset when this method returns (after the
        // matching rule's SaveChanges), so a subsequent
        // user-originated event flows normally.
        bool previous = _inAutomationBroadcast.Value;
        _inAutomationBroadcast.Value = true;
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

            BoardListId effectiveListId = listId ?? card.ListId;
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
                await ExecuteActionAsync(rule, card, unitOfWork, _clock, _logger, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AutomationEventBroadcaster failed while processing {Trigger} for card {CardId}",
                trigger, cardId);
            throw;
        }
        finally
        {
            // BETA-A7-R2-001 — restore the prior flag so a
            // sibling call from a different request isn't
            // poisoned by our re-entry.
            _inAutomationBroadcast.Value = previous;
        }
    }

    private static async Task ExecuteActionAsync(
        BoardAutomationRule rule,
        Card card,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            switch (rule.Action)
            {
                case AutomationAction.MoveCardToList when Guid.TryParse(rule.ActionArgument, out var listId):
                    {
                        var result = card.Move(new BoardListId(listId), card.Position, clock.UtcNow);
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
                        var result = card.Assign(userId, clock.UtcNow);
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
                        var result = card.SetDueDate(due, clock.UtcNow);
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
                        var result = card.Complete(clock.UtcNow);
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
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Automation rule {RuleId} applied {Action} to card {CardId}",
                    rule.Id, rule.Action, card.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automation rule {RuleId} threw on card {CardId}", rule.Id, card.Id);
            throw;
        }
    }
}
