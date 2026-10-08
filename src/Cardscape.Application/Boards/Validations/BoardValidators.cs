using Cardscape.Application.Boards.Commands;
using Cardscape.Domain.Boards;
using FluentValidation;

namespace Cardscape.Application.Boards.Validations;

public sealed class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
    public CreateBoardCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BoardName.MaxLength);
    }
}

public sealed class RenameBoardCommandValidator : AbstractValidator<RenameBoardCommand>
{
    public RenameBoardCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(BoardName.MaxLength);
    }
}
