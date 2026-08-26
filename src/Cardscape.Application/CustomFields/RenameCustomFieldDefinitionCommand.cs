using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public sealed record RenameCustomFieldDefinitionCommand(Guid FieldId, string NewName) : IMessage;

public static class RenameCustomFieldDefinitionCommandHandler
{
    public static async Task<Result<CustomFieldDefinitionDto>> Handle(
        RenameCustomFieldDefinitionCommand command,
        ICustomFieldDefinitionRepository definitions,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CustomFieldDefinitionDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        CustomFieldDefinition? field = await definitions.GetByIdAsync(
            new CustomFieldDefinitionId(command.FieldId), cancellationToken);
        if (field is null)
        {
            return Result.Failure<CustomFieldDefinitionDto>(DomainError.NotFound(
                "custom_fields.not_found", "Field was not found."));
        }

        if (!await CustomFieldGuards.CanEditAsync(boards, field.BoardId, currentUser.Id.Value, cancellationToken))
        {
            return Result.Failure<CustomFieldDefinitionDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var rename = field.Rename(command.NewName, clock.UtcNow);
        if (rename.IsFailure)
        {
            return Result.Failure<CustomFieldDefinitionDto>(rename.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CustomFieldDefinitionDto.FromEntity(field));
    }
}
