using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
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

        // Record creation on the activity feed. Label creation has
        // no dedicated ActivityKind, so we reuse CardRenamed
        // as the closest stand-in (a follow-up PR can add a
        // dedicated LabelCreated kind).
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


