using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public sealed record CreateCustomFieldDefinitionCommand(
    Guid BoardId,
    string Name,
    int Kind,
    IReadOnlyList<string>? DropdownOptions,
    int Position) : IMessage;

public static class CreateCustomFieldDefinitionCommandHandler
{
    public static async Task<Result<CustomFieldDefinitionDto>> Handle(
        CreateCustomFieldDefinitionCommand command,
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

        if (!Enum.IsDefined((CustomFieldKind)command.Kind))
        {
            return Result.Failure<CustomFieldDefinitionDto>(DomainError.Validation(
                "custom_fields.kind_unknown", $"Unknown custom field kind: {command.Kind}."));
        }

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<CustomFieldDefinitionDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<CustomFieldDefinitionDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var creation = CustomFieldDefinition.Create(
            board.Id,
            command.Name,
            (CustomFieldKind)command.Kind,
            command.DropdownOptions,
            command.Position,
            clock.UtcNow);

        if (creation.IsFailure)
        {
            return Result.Failure<CustomFieldDefinitionDto>(creation.Error);
        }

        await definitions.AddAsync(creation.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CustomFieldDefinitionDto.FromEntity(creation.Value));
    }
}
