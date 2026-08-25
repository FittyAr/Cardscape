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

        // Record the deletion on the activity feed.
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


