using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Recurrence;
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
            logger.LogWarning("CloneCardJob {JobId} missing or invalid cardId.", jobId);
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
            logger.LogWarning("CloneCardJob: source card {CardId} not found.", cardGuid);
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

        var clone = Card.Create(
            CardId.New(),
            source.ListId,
            source.Title,
            source.Description,
            Position.From(nextPos),
            source.CreatedBy ?? Guid.Empty,
            clock.UtcNow);
        if (clone.IsFailure)
        {
            logger.LogWarning(
                "CloneCardJob: failed to create clone for {CardId}: {Code} {Msg}",
                source.Id.Value, clone.Error.Code, clone.Error.Message);
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
        logger.LogInformation(
            "Cloned card {SourceId} -> {CloneId} via recurrence.",
            source.Id.Value, clone.Value.Id.Value);
    }
}
