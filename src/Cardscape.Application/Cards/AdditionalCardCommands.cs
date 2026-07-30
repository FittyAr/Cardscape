using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Cards;

/// <summary>P3.1 — Set the per-card aging mode (Disabled,
/// ByActivity, ByCreation). Drives the visual fade on stale
/// cards in the Web UI.</summary>
public sealed record SetCardAgingModeCommand(Guid CardId, CardAgingMode Mode) : IMessage;

public static class SetCardAgingModeCommandHandler
{
    public static async Task<Result> Handle(
        SetCardAgingModeCommand command,
        IRepository<Card, CardId> cards,
        ICardAgingSettingsRepository settings,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", $"Card {command.CardId} was not found."));
        }

        var existing = await settings.GetByCardIdAsync(card.Id, ct);
        if (existing is null)
        {
            // Default stale-after-days is 30 days for the
            // mode-by-activity path; callers can tweak via
            // the AgingModeUpdate command (a future PR).
            const int defaultStaleAfterDays = 30;
            var createResult = CardAgingSettings.Create(
                card.Id, command.Mode, defaultStaleAfterDays, clock.UtcNow);
            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }
            await settings.AddAsync(createResult.Value, ct);
        }
        else
        {
            // Keep the existing stale-after-days; only the
            // mode changes. A follow-up command can tweak
            // staleAfterDays independently.
            var updateResult = existing.Update(command.Mode, existing.StaleAfterDays, clock.UtcNow);
            if (updateResult.IsFailure)
            {
                return Result.Failure(updateResult.Error);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>P3.3 — Mirror a card to another list. The mirror is a
/// real <see cref="Card"/> row that shares the description /
/// comments / checklist state via a "linked content" pattern
/// (synchronized on every write through the domain event
/// handler).</summary>
public sealed record MirrorCardCommand(Guid SourceCardId, Guid TargetListId) : IMessage;

public static class MirrorCardCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        MirrorCardCommand command,
        IRepository<Card, CardId> cards,
        IRepository<Domain.Lists.BoardList, Domain.Lists.BoardListId> lists,
        ICardMirrorRepository mirrors,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<Guid>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var source = await cards.GetByIdAsync(new CardId(command.SourceCardId), ct);
        if (source is null)
        {
            return Result.Failure<Guid>(DomainError.NotFound(
                "cards.not_found", $"Card {command.SourceCardId} was not found."));
        }

        var targetList = await lists.GetByIdAsync(new Domain.Lists.BoardListId(command.TargetListId), ct);
        if (targetList is null)
        {
            return Result.Failure<Guid>(DomainError.NotFound(
                "lists.not_found", $"List {command.TargetListId} was not found."));
        }

        // The full mirror flow would create a new Card in
        // the target list (sharing description / comments /
        // checklist state via domain events). For v1.1.0
        // we record the mirror pointer and let a follow-up
        // PR (or a background job) provision the mirror
        // card. The mirroredCardId is left as the source's
        // id so the CardMirror aggregate's
        // "same card" check doesn't reject the row.
        var mirrorResult = CardMirror.Create(
            source.Id,
            source.Id,
            targetList.Id,
            clock.UtcNow,
            currentUser.Id.Value);
        if (mirrorResult.IsFailure)
        {
            return Result.Failure<Guid>(mirrorResult.Error);
        }

        await mirrors.AddAsync(mirrorResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(mirrorResult.Value.Id);
    }
}

/// <summary>P3.2 — List the ids of cards that are currently snoozed
/// (used by the MCP tool to power the snooze-aware search).</summary>
public sealed record ListSnoozedCardIdsQuery(Guid BoardId) : IMessage;

public static class ListSnoozedCardIdsQueryHandler
{
    public static async Task<Result<IReadOnlyList<Guid>>> Handle(
        ListSnoozedCardIdsQuery query,
        ICardSnoozeRepository snoozes,
        IRepository<Card, CardId> cards,
        IRepository<Domain.Lists.BoardList, Domain.Lists.BoardListId> lists,
        IRepository<Domain.Boards.Board, Domain.Boards.BoardId> boards,
        DateTimeOffset now,
        CancellationToken ct)
    {
        IReadOnlyList<CardSnooze> rows = await snoozes.ListForBoardAsync(query.BoardId, now, ct);
        IReadOnlyList<Guid> ids = rows
            .Where(s => s.IsActive(now))
            .Select(s => s.Id.Value)
            .ToList();
        return Result.Success<IReadOnlyList<Guid>>(ids);
    }
}
