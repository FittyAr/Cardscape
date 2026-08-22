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

public sealed record CompleteCardCommand(Guid CardId) : IMessage;

public static class CompleteCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        CompleteCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
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

        var result = card.Complete(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the completion on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardMoved, // CardCompleted reuses CardMoved until a dedicated kind is added.
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}

public sealed record ReopenCardCommand(Guid CardId) : IMessage;

public static class ReopenCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ReopenCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
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

        var result = card.Reopen(clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<CardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the reopen on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardMoved, // Reopen reuses CardMoved until a dedicated kind is added.
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}

public sealed record ArchiveCardCommand(Guid CardId) : IMessage;

public static class ArchiveCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        ArchiveCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
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

        card.Archive(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the archive on the activity feed.
        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardArchived,
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}

public sealed record DeleteCardCommand(Guid CardId) : IMessage;

public static class DeleteCardCommandHandler
{
    public static async Task<Result> Handle(
        DeleteCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IAttachmentRepository attachments,
        IStorageService storage,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure(NotFound);
        }

        var guard = await MembershipGuards.EnsureCanMutateCardAsync(
            card, lists, boards, currentUser.Id.Value, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure(guard.Error);
        }

        Guid boardId = guard.Value.Board.Id.Value;

        // BETA-5-R2-006 — see
        // test-results/beta/round-2/reports/A5-card-extras.md.
        // The previous handler only removed the Card row. The
        // attachment files (stored on disk under /app/Storage
        // by the IStorageService) survived the delete and
        // were orphaned. Capture the attachment list + storage
        // keys first, then the EF Core cascade rule wipes the
        // metadata rows, then the storage backend best-effort
        // deletes the blobs so the disk cannot grow forever
        // even when no one bothers to call the per-row
        // delete.
        IReadOnlyList<Attachment> attachmentRows = await attachments.ListForCardAsync(
            command.CardId, cancellationToken);
        var storageKeys = attachmentRows.Select(a => a.StorageKey).ToList();

        // BETA-7-#2 — record the deletion before the card is
        // removed from the DB. The CardId is still valid
        // here; once the row is gone the activity feed would
        // hold a dangling foreign-key-like reference.
        await activities.AddAsync(Activity.Create(
            new Domain.Boards.BoardId(boardId),
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardArchived, // CardDeleted reuses CardArchived until a dedicated kind is added.
            $"{{\"title\":\"{card.Title.Value.Replace("\"", "\\\"")}\"}}",
            clock.UtcNow), cancellationToken);

        cards.Remove(card);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Best-effort blob cleanup. The metadata rows are
        // gone by now (EF Core cascade), so a stale blob is
        // harmless next to the row.
        foreach (string key in storageKeys)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch
            {
                // see DeleteAttachmentCommandHandler — same
                // policy.
            }
        }

        return Result.Success();
    }
}

public sealed record RestoreCardCommand(Guid CardId) : IMessage;

public static class RestoreCardCommandHandler
{
    public static async Task<Result<CardDto>> Handle(
        RestoreCardCommand command,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
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

        card.Restore(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await activities.AddAsync(Activity.Create(
            guard.Value.Board.Id,
            card.Id.Value,
            currentUser.Id.Value,
            ActivityKind.CardRestored,
            "{}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(card.MapToDto());
    }
}
