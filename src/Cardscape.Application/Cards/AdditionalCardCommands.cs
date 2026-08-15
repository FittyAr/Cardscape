using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
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
        IBoardListRepository lists,
        IBoardRepository boards,
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

        // v1.2.0 audit (pass 12): the previous incarnation
        // had no board-membership check. The MCP
        // `cards_set_aging_mode` tool and any code that
        // routed through this command could mutate the
        // aging mode of any card by guessing the id. The
        // fix reuses the same MembershipGuards helper the
        // card write paths already use.
        var card = await cards.GetByIdAsync(new CardId(command.CardId), ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", $"Card {command.CardId} was not found."));
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, ct);
        if (guard.IsFailure)
        {
            return Result.Failure(guard.Error);
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

/// <summary>P3.2 — List the ids of cards that are currently snoozed
/// (used by the MCP tool to power the snooze-aware search).</summary>
public sealed record ListSnoozedCardIdsQuery(Guid BoardId) : IMessage;

public static class ListSnoozedCardIdsQueryHandler
{
    public static async Task<Result<IReadOnlyList<Guid>>> Handle(
        ListSnoozedCardIdsQuery query,
        ICardSnoozeRepository snoozes,
        IRepository<Card, CardId> cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<Guid>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // v1.2.0 audit (pass 12): the previous incarnation
        // had no auth and no membership check. The MCP
        // `cards_list_snoozed` tool and the HTTP
        // `/api/boards/{id}/snoozed` endpoint would both
        // hand back the snoozed-card ids of any board by
        // guessing the id.
        var board = await boards.GetWithMembersAsync(
            new Domain.Boards.BoardId(query.BoardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<Guid>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<CardSnooze> rows = await snoozes.ListForBoardAsync(query.BoardId, now, ct);
        IReadOnlyList<Guid> ids = rows
            .Where(s => s.IsActive(now))
            .Select(s => s.Id.Value)
            .ToList();
        return Result.Success<IReadOnlyList<Guid>>(ids);
    }
}
