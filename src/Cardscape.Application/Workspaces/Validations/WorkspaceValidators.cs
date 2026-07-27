using Cardscape.Application.Workspaces.Commands;
using FluentValidation;

namespace Cardscape.Application.Workspaces.Validations;

public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class RenameWorkspaceCommandValidator : AbstractValidator<RenameWorkspaceCommand>
{
    public RenameWorkspaceCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(100);
    }
}
