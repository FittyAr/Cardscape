using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public sealed record ListCustomFieldValuesForCardQuery(Guid CardId) : IMessage;

public static class ListCustomFieldValuesForCardQueryHandler
{
    public static async Task<Result<IReadOnlyList<CustomFieldValueDto>>> HandleAsync(
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
        if (rows.Count == 0 || await lists.GetByIdAsync(card.ListId, cancellationToken) is not { } list)
        {
            return Result.Success<IReadOnlyList<CustomFieldValueDto>>([]);
        }

        // One round-trip for the board's definitions instead of one per value.
        Dictionary<CustomFieldDefinitionId, CustomFieldDefinition> fields =
            (await definitions.ListForBoardAsync(list.BoardId, cancellationToken)).ToDictionary(f => f.Id);
        List<CustomFieldValueDto> dtos =
        [
            .. rows
                .Where(v => fields.ContainsKey(v.FieldDefinitionId))
                .Select(v => CustomFieldValueDto.FromEntity(v, fields[v.FieldDefinitionId]))
                .OrderBy(dto => dto.Position),
        ];
        return Result.Success<IReadOnlyList<CustomFieldValueDto>>(dtos);
    }
}

// ── shared helpers ─────────────────────────────────────────

