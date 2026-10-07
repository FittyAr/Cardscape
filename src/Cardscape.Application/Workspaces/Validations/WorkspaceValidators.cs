using Cardscape.Application.Workspaces.Commands;
using Cardscape.Domain.Workspaces;
using FluentValidation;

namespace Cardscape.Application.Workspaces.Validations;

public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(WorkspaceName.MaxLength);
    }
}

public sealed class RenameWorkspaceCommandValidator : AbstractValidator<RenameWorkspaceCommand>
{
    public RenameWorkspaceCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(WorkspaceName.MaxLength);
    }
}
