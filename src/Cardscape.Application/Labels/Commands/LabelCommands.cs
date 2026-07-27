using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using MediatR;
using static Cardscape.Domain.Labels.Errors.LabelErrors;

namespace Cardscape.Application.Labels.Commands;

public sealed record CreateLabelCommand(Guid BoardId, string Name, string Color)
    : IRequest<Result<LabelDto>>;

public sealed class CreateLabelCommandHandler(
    ILabelRepository labels,
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
    public async Task<Result<LabelDto>> Handle(
        CreateLabelCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<LabelDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
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

        var nameResult = LabelName.Create(request.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<LabelDto>(nameResult.Error);
        }

        var colorResult = Color.Create(request.Color);
        if (colorResult.IsFailure)
        {
            return Result.Failure<LabelDto>(colorResult.Error);
        }

        var labelResult = Label.Create(
            LabelId.New(),
            new BoardId(request.BoardId),
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

        return Result.Success(new LabelDto(
            labelResult.Value.Id.Value,
            labelResult.Value.BoardId.Value,
            labelResult.Value.Name.Value,
            labelResult.Value.Color.Value));
    }
}

public sealed record UpdateLabelCommand(Guid LabelId, string Name, string Color)
    : IRequest<Result<LabelDto>>;

public sealed class UpdateLabelCommandHandler(
    ILabelRepository labels,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<UpdateLabelCommand, Result<LabelDto>>
{
    public async Task<Result<LabelDto>> Handle(
        UpdateLabelCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<LabelDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var label = await labels.GetByIdAsync(new LabelId(request.LabelId), cancellationToken);
        if (label is null)
        {
            return Result.Failure<LabelDto>(NotFound);
        }

        var nameResult = LabelName.Create(request.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<LabelDto>(nameResult.Error);
        }

        var colorResult = Color.Create(request.Color);
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
        return Result.Success(new LabelDto(
            label.Id.Value,
            label.BoardId.Value,
            label.Name.Value,
            label.Color.Value));
    }
}

public sealed record DeleteLabelCommand(Guid LabelId) : IRequest<Result>;

public sealed class DeleteLabelCommandHandler(
    ILabelRepository labels,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<DeleteLabelCommand, Result>
{
    public async Task<Result> Handle(
        DeleteLabelCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var label = await labels.GetByIdAsync(new LabelId(request.LabelId), cancellationToken);
        if (label is null)
        {
            return Result.Failure(NotFound);
        }

        label.Delete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
