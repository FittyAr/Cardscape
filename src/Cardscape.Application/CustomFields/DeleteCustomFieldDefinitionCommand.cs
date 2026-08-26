using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public sealed record DeleteCustomFieldDefinitionCommand(Guid FieldId) : IMessage;

public static class DeleteCustomFieldDefinitionCommandHandler
{
    public static async Task<Result> Handle(
        DeleteCustomFieldDefinitionCommand command,
        ICustomFieldDefinitionRepository definitions,
        ICustomFieldValueRepository values,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        CustomFieldDefinition? field = await definitions.GetByIdAsync(
            new CustomFieldDefinitionId(command.FieldId), cancellationToken);
        if (field is null)
        {
            return Result.Failure(DomainError.NotFound(
                "custom_fields.not_found", "Field was not found."));
        }

        if (!await CustomFieldGuards.CanEditAsync(boards, field.BoardId, currentUser.Id.Value, cancellationToken))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<CustomFieldValue> affected = await values.ListForFieldAsync(field.Id, cancellationToken);
        foreach (CustomFieldValue value in affected)
        {
            values.Remove(value);
        }

        definitions.Remove(field);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
