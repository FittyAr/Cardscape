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

public sealed record SetCustomFieldValueCommand(
    Guid CardId,
    Guid FieldId,
    string? ValueJson) : IMessage;

public static class SetCustomFieldValueCommandHandler
{
    public static async Task<Result<CustomFieldValueDto>> Handle(
        SetCustomFieldValueCommand command,
        ICustomFieldValueRepository values,
        ICustomFieldDefinitionRepository definitions,
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CustomFieldValueDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        CustomFieldDefinition? field = await definitions.GetByIdAsync(
            new CustomFieldDefinitionId(command.FieldId), cancellationToken);
        if (field is null)
        {
            return Result.Failure<CustomFieldValueDto>(DomainError.NotFound(
                "custom_fields.not_found", "Field was not found."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CustomFieldValueDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        if (!await CustomFieldGuards.CanEditAsync(boards, field.BoardId, currentUser.Id.Value, cancellationToken))
        {
            return Result.Failure<CustomFieldValueDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        // The previous incarnation only checked the field's
        // board. A user who is a member of board A could
        // write a value for a board-A field onto a card
        // living on board B (which they cannot see) by
        // guessing the card id. The v1.2.0 audit (pass 12)
        // adds the second check: the card must live on a
        // list that belongs to the same board as the field.
        if (!await CustomFieldGuards.CardBelongsToBoardAsync(
                lists, card, field.BoardId, cancellationToken))
        {
            return Result.Failure<CustomFieldValueDto>(DomainError.Forbidden(
                "boards.card_not_in_board",
                "Card does not belong to the field's board."));
        }

        CustomFieldValue? existing = await values.GetByFieldAndCardAsync(
            field.Id, card.Id, cancellationToken);

        if (string.IsNullOrEmpty(command.ValueJson))
        {
            if (existing is not null)
            {
                values.Remove(existing);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new CustomFieldValueDto(
                field.Id.Value, card.Id.Value, (int)field.Kind, string.Empty));
        }

        Result shape = CustomFieldValue.ValidateShape(command.ValueJson, field.Kind);
        if (shape.IsFailure)
        {
            return Result.Failure<CustomFieldValueDto>(shape.Error);
        }

        if (field.Kind == CustomFieldKind.Dropdown)
        {
            Result dropdown = ValidateDropdownAgainstOptions(command.ValueJson, field.OptionsJson);
            if (dropdown.IsFailure)
            {
                return Result.Failure<CustomFieldValueDto>(dropdown.Error);
            }
        }

        if (existing is null)
        {
            var creation = CustomFieldValue.Create(field.Id, card.Id, command.ValueJson, clock.UtcNow);
            if (creation.IsFailure)
            {
                return Result.Failure<CustomFieldValueDto>(creation.Error);
            }
            await values.AddAsync(creation.Value, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(CustomFieldValueDto.FromEntity(creation.Value, field.Kind));
        }

        var update = existing.SetValue(command.ValueJson, field.Kind, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<CustomFieldValueDto>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CustomFieldValueDto.FromEntity(existing, field.Kind));
    }

    private static Result ValidateDropdownAgainstOptions(string valueJson, string optionsJson)
    {
        try
        {
            using JsonDocument valueDoc = JsonDocument.Parse(valueJson);
            string? selected = valueDoc.RootElement.GetString();
            if (string.IsNullOrEmpty(selected))
            {
                return Result.Success();
            }

            string[] options = JsonSerializer.Deserialize<string[]>(optionsJson) ?? [];
            if (!options.Contains(selected, StringComparer.Ordinal))
            {
                return Result.Failure(DomainError.Validation(
                    "custom_fields.dropdown_value_unknown",
                    $"'{selected}' is not a valid dropdown option."));
            }
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.Validation(
                "custom_fields.value_not_dropdown", ex.Message));
        }
    }
}

public sealed record ListCustomFieldValuesForCardQuery(Guid CardId) : IMessage;

public static class ListCustomFieldValuesForCardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CustomFieldValueDto>>> Handle(
        ListCustomFieldValuesForCardQuery query,
        ICustomFieldValueRepository values,
        ICustomFieldDefinitionRepository definitions,
        ICardRepository cards,
        IBoardRepository boards,
        IBoardListRepository lists,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CustomFieldValueDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<IReadOnlyList<CustomFieldValueDto>>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        if (!await CustomFieldGuards.CanReadCardAsync(
                boards, lists, card, currentUser.Id.Value, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CustomFieldValueDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<CustomFieldValue> rows = await values.ListForCardAsync(card.Id, cancellationToken);
        var dtos = new List<CustomFieldValueDto>();
        foreach (CustomFieldValue v in rows)
        {
            CustomFieldDefinition? field = await definitions.GetByIdAsync(v.FieldDefinitionId, cancellationToken);
            if (field is null)
            {
                continue;
            }
            dtos.Add(CustomFieldValueDto.FromEntity(v, field.Kind));
        }
        return Result.Success<IReadOnlyList<CustomFieldValueDto>>(dtos);
    }
}

// ── shared helpers ─────────────────────────────────────────

public static class CustomFieldGuards
{
    public static async Task<bool> CanEditAsync(
        IBoardRepository boards, BoardId boardId, Guid userId, CancellationToken ct)
    {
        Board? board = await boards.GetWithMembersAsync(boardId, ct);
        return board is not null && board.IsMember(userId);
    }

    public static async Task<bool> CanReadCardAsync(
        IBoardRepository boards,
        IBoardListRepository lists,
        Card card,
        Guid userId,
        CancellationToken ct)
    {
        // Resolve the card's list → board id, then check membership.
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        if (!map.TryGetValue(card.ListId.Value, out Guid boardId))
        {
            return false;
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId), ct);
        return board is not null && board.IsMember(userId);
    }

    /// <summary>
    /// True when the card's list belongs to <paramref name="expectedBoardId"/>.
    /// The v1.2.0 audit (pass 12) uses this as a second
    /// line of defence in <c>SetCustomFieldValueCommandHandler</c>
    /// so a value can never be written onto a card that
    /// lives in a board different from the field's board.
    /// </summary>
    public static async Task<bool> CardBelongsToBoardAsync(
        IBoardListRepository lists,
        Card card,
        BoardId expectedBoardId,
        CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, Guid> map = await lists.ListBoardIdsByListIdAsync(ct);
        return map.TryGetValue(card.ListId.Value, out Guid boardId)
            && boardId == expectedBoardId.Value;
    }
}

// ── DTOs ─────────────────────────────────────────────────────

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid BoardId,
    string Name,
    int Kind,
    string OptionsJson,
    int Position)
{
    public static CustomFieldDefinitionDto FromEntity(CustomFieldDefinition d) => new(
        d.Id.Value, d.BoardId.Value, d.Name, (int)d.Kind, d.OptionsJson, d.Position);
}

public sealed record CustomFieldValueDto(
    Guid FieldDefinitionId,
    Guid CardId,
    int Kind,
    string ValueJson)
{
    public static CustomFieldValueDto FromEntity(CustomFieldValue v, CustomFieldKind kind) =>
        new(v.FieldDefinitionId.Value, v.CardId.Value, (int)kind, v.ValueJson);
}
