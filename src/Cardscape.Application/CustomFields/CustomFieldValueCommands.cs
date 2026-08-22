using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

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
