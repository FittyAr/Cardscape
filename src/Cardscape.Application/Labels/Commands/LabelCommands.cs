using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Wolverine;
using static Cardscape.Domain.Labels.Errors.LabelErrors;

namespace Cardscape.Application.Labels.Commands;

public sealed record CreateLabelCommand(Guid BoardId, string Name, string Color)
    : IMessage;

public static class CreateLabelCommandHandler
{
    public static async Task<Result<LabelDto>> Handle(
        CreateLabelCommand command,
        IBoardRepository boards,
        ILabelRepository labels,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        ISearchIndex searchIndex,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<LabelDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<LabelDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<LabelDto>(DomainError.Forbidden(
                "boards.not_member", "You are not a member of this board."));
        }

        var nameResult = LabelName.Create(command.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<LabelDto>(nameResult.Error);
        }

        var colorResult = Color.Create(command.Color);
        if (colorResult.IsFailure)
        {
            return Result.Failure<LabelDto>(colorResult.Error);
        }

        var labelResult = Label.Create(
            LabelId.New(),
            new BoardId(command.BoardId),
            nameResult.Value,
            colorResult.Value,
            currentUser.Id.Value,
            clock.UtcNow);

        if (labelResult.IsFailure)
        {
            return Result.Failure<LabelDto>(labelResult.Error);
        }

        await labels.AddAsync(labelResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#1 / #2 — index the label and record the
        // creation on the activity feed. Label creation has
        // no dedicated ActivityKind, so we reuse CardRenamed
        // as the closest stand-in (a follow-up PR can add a
        // dedicated LabelCreated kind).
        await searchIndex.IndexLabelAsync(labelResult.Value, cancellationToken);
        await activities.AddAsync(Activity.Create(
            labelResult.Value.BoardId,
            null,
            currentUser.Id.Value,
            ActivityKind.CardRenamed,
            $"{{\"labelId\":\"{labelResult.Value.Id.Value}\",\"name\":\"{labelResult.Value.Name.Value.Replace("\"", "\\\"")}\"}}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new LabelDto(
            labelResult.Value.Id.Value,
            labelResult.Value.BoardId.Value,
            labelResult.Value.Name.Value,
            labelResult.Value.Color.Value));
    }
}

public sealed record UpdateLabelCommand(Guid LabelId, string Name, string Color)
    : IMessage;

public static class UpdateLabelCommandHandler
{
    public static async Task<Result<LabelDto>> Handle(
        UpdateLabelCommand command,
        ILabelRepository labels,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        ISearchIndex searchIndex,
        IActivityRepository activities,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<LabelDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var label = await labels.GetByIdAsync(new LabelId(command.LabelId), cancellationToken);
        if (label is null)
        {
            return Result.Failure<LabelDto>(NotFound);
        }

        var nameResult = LabelName.Create(command.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<LabelDto>(nameResult.Error);
        }

        var colorResult = Color.Create(command.Color);
        if (colorResult.IsFailure)
        {
            return Result.Failure<LabelDto>(colorResult.Error);
        }

        var updateResult = label.Update(nameResult.Value, colorResult.Value, clock.UtcNow);
        if (updateResult.IsFailure)
        {
            return Result.Failure<LabelDto>(updateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#1 / #2 — re-index the updated label and
        // record the update on the activity feed.
        await searchIndex.IndexLabelAsync(label, cancellationToken);
        await activities.AddAsync(Activity.Create(
            label.BoardId,
            null,
            currentUser.Id.Value,
            ActivityKind.CardRenamed, // LabelUpdated reuses CardRenamed until a dedicated kind is added.
            $"{{\"labelId\":\"{label.Id.Value}\",\"name\":\"{label.Name.Value.Replace("\"", "\\\"")}\"}}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new LabelDto(
            label.Id.Value,
            label.BoardId.Value,
            label.Name.Value,
            label.Color.Value));
    }
}

public sealed record DeleteLabelCommand(Guid LabelId) : IMessage;

public static class DeleteLabelCommandHandler
{
    public static async Task<Result> Handle(
        DeleteLabelCommand command,
        ILabelRepository labels,
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

        var label = await labels.GetByIdAsync(new LabelId(command.LabelId), cancellationToken);
        if (label is null)
        {
            return Result.Failure(NotFound);
        }

        label.Delete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // BETA-7-#2 — record the deletion on the activity feed.
        // The in-memory search index is process-wide and
        // doesn't currently support RemoveLabelAsync; the
        // label hit stays in the index until the next process
        // restart. The hit is filtered out at search time by
        // the soft-delete check (the label is gone from the DB
        // and won't surface in any label picker / kanban
        // card decoration). A follow-up can wire up
        // RemoveLabelAsync on the ISearchIndex.
        await activities.AddAsync(Activity.Create(
            label.BoardId,
            null,
            currentUser.Id.Value,
            ActivityKind.CardRenamed, // LabelDeleted reuses CardRenamed until a dedicated kind is added.
            $"{{\"labelId\":\"{label.Id.Value}\"}}",
            clock.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
