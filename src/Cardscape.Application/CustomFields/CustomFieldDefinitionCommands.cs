using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
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

        if (!Enum.IsDefined(typeof(CustomFieldKind), command.Kind))
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

        // Cascade: remove every value that references this field.
        IReadOnlyList<CustomFieldValue> affected = await values.ListForBoardAsync(field.BoardId, cancellationToken);
        foreach (CustomFieldValue v in affected.Where(v => v.FieldDefinitionId == field.Id))
        {
            values.Remove(v);
        }

        definitions.Remove(field);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListCustomFieldDefinitionsQuery(Guid BoardId) : IMessage;

public static class ListCustomFieldDefinitionsQueryHandler
{
    public static async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> Handle(
        ListCustomFieldDefinitionsQuery query,
        ICustomFieldDefinitionRepository definitions,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<CustomFieldDefinition> rows =
            await definitions.ListForBoardAsync(board.Id, cancellationToken);
        return Result.Success<IReadOnlyList<CustomFieldDefinitionDto>>(
            rows.Select(CustomFieldDefinitionDto.FromEntity).ToList());
    }
}
