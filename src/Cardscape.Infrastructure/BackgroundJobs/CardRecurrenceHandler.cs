using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Recurrence;
using Cardscape.Infrastructure.Logging;
using Cardscape.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.BackgroundJobs;

/// <summary>Background job type identifier for the recurring-card
/// clone. The dispatcher claims <c>BackgroundJob</c> rows whose
/// <see cref="Domain.BackgroundJobs.BackgroundJob.Type"/> matches
/// this constant and hands them to
/// <see cref="CloneCardHandler"/>.</summary>
public static class RecurringCardJobTypes
{
    public const string CloneCard = "recurring-card.clone";
}

/// <summary>Payload of a recurring-card clone job. Kept tiny on
/// purpose: the handler reloads the card fresh from the
/// repository, so the payload is just an identifier and the
/// scheduled time.</summary>
public sealed record CloneCardJobPayload(Guid CardId, DateTimeOffset ScheduledFor);

/// <summary>Clones a card onto the same list when its recurrence
/// rule fires. The new card is created with the same title and
/// description, marked not-completed, and gets a new position
/// (end of the list). The recurrence's <c>NextOccurrenceAt</c>
/// is then advanced by the rule's interval so the dispatcher
/// picks it up again later.</summary>
public sealed class CloneCardHandler(
    IServiceScopeFactory scopes,
    ILogger<CloneCardHandler> logger) : IBackgroundJobHandler
{
    public string Type => RecurringCardJobTypes.CloneCard;

    public async Task HandleAsync(
        Guid jobId, JsonElement payload, CancellationToken ct)
    {
        if (!payload.TryGetProperty("cardId", out JsonElement idElement)
            || !idElement.TryGetGuid(out Guid cardGuid))
        {
            logger.CloneJobCardIdInvalid(jobId);
            return;
        }

        using IServiceScope scope = scopes.CreateScope();
        var cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        var lists = scope.ServiceProvider.GetRequiredService<IBoardListRepository>();
        var recurrences = scope.ServiceProvider.GetRequiredService<ICardRecurrenceRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        Card? source = await cards.GetByIdAsync(new CardId(cardGuid), ct);
        if (source is null)
        {
            logger.CloneSourceNotFound(cardGuid);
            return;
        }

        // The source card is archived. A user who archived the
        // source explicitly wants the recurrence paused; the
        // same row will reappear if they restore. Skipping the
        // clone is the user-aligned behaviour. The recurrence's
        // NextOccurrenceAt is NOT advanced so the next tick
        // still has a chance to clone once the user restores
        // the card (or restores the parent list).
        if (source.IsArchived)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.CloneSourceArchived(cardGuid);
            }
            return;
        }

        // The parent list might be archived even if the card
        // itself is not (a list archive cascades only on the
        // list header, leaving the cards in a quiescent state
        // for the restore flow). Cloning into an archived
        // list is a UX bug: the cloned card would be
        // invisible to the user until the list is restored.
        BoardList? parentList = await lists.GetByIdAsync(source.ListId, ct);
        if (parentList is null)
        {
            logger.CloneParentListMissing(cardGuid);
            return;
        }

        if (parentList.IsArchived)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.CloneParentListArchived(cardGuid);
            }
            return;
        }

        // Find the list's current max position so the clone goes
        // to the end. The in-memory list is one of a kind, so a
        // single linear scan is fine.
        IReadOnlyList<Card> sameList = await cards.ListForListAsync(
            source.ListId, includeArchived: false, ct);
        double nextPos = sameList.Count == 0
            ? Position.Start().Value
            : sameList.Max(c => c.Position.Value) + 1;

        // The source card was created by a real user. The
        // CreatedBy Guid is non-null on every card written
        // through the regular Create path (Card.Create rejects
        // Guid.Empty), so a null value here is a data-integrity
        // anomaly we should surface, not paper over with
        // Guid.Empty. Skipping the clone is safer than creating
        // a card with a meaningless CreatedBy that downstream
        // audit and ownership checks would misattribute.
        if (source.CreatedBy is null || source.CreatedBy.Value == Guid.Empty)
        {
            logger.CloneCreatorMissing(source.Id.Value);
            return;
        }

        var clone = Card.Create(
            CardId.New(),
            source.ListId,
            source.Title,
            source.Description,
            Position.From(nextPos),
            source.CreatedBy.Value,
            clock.UtcNow);
        if (clone.IsFailure)
        {
            logger.CloneCreationFailed(source.Id.Value, clone.Error.Code, clone.Error.Message);
            return;
        }

        await cards.AddAsync(clone.Value, ct);

        // Reschedule the rule for the next occurrence.
        CardRecurrence? rule = await recurrences.GetForCardAsync(source.Id, ct);
        if (rule is not null && rule.IsActive)
        {
            rule.Reschedule(clock.UtcNow.AddDays(rule.IntervalDays));
        }

        await uow.SaveChangesAsync(ct);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.CardCloned(source.Id.Value, clone.Value.Id.Value);
        }
    }
}
