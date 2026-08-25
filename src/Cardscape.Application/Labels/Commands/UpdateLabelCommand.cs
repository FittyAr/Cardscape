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

        // Record the update on the activity feed.
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


