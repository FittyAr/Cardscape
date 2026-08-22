using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Cards.Common;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Wolverine;
using static Cardscape.Domain.Cards.Errors.CardErrors;
using Color = Cardscape.Domain.Common.Color;

namespace Cardscape.Application.Cards.Commands;

public sealed record SetCardDueDateCommand(Guid CardId, DateTimeOffset DueDate)
    : IMessage;

public static class SetCardDueDateCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        SetCardDueDateCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        var result = card.SetDueDate(command.DueDate, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

public sealed record ClearCardDueDateCommand(Guid CardId) : IMessage;

public static class ClearCardDueDateCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ClearCardDueDateCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        var result = card.ClearDueDate(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}

// BETA-A4-009 — see test-results/beta/round-2/reports/A4-cards-lists.md.
// The Card aggregate has supported SetCoverColor(Color?) since the
// initial release, but the API surface never exposed the
// corresponding command / endpoint. The card detail UI and the
// drag-handle "Cover" picker both relied on a /cover route that
// 404'd. Add the two endpoints now so the existing domain method
// is reachable.
public sealed record SetCardCoverCommand(Guid CardId, string? ColorName) : IMessage;

public static class SetCardCoverCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        SetCardCoverCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<CardDto>(guard.Error);
        }

        // null / empty / "none" all clear the cover (one
        // operation covers set + clear). Otherwise look up the
        // Color by name in the palette and set it; an unknown
        // name is a 400 instead of a silent miss.
        Color? cover = null;
        if (!string.IsNullOrWhiteSpace(command.ColorName)
            && !string.Equals(command.ColorName, "none", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = Cardscape.Domain.Common.Color.Palette.ByName(command.ColorName);
            if (parsed is null)
            {
                return Result.Failure<CardDto>(DomainError.Validation(
                    "cards.cover_invalid",
                    $"Cover color '{command.ColorName}' is not a known palette colour."));
            }
            cover = parsed;
        }

        var result = card.SetCoverColor(cover, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(card.MapToDto());
    }
}
