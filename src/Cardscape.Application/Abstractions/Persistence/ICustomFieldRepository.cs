using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="CustomFieldDefinition"/>.
/// The natural key is <c>(BoardId, Name)</c>: a board cannot have
/// two fields with the same name.
/// </summary>
public interface ICustomFieldDefinitionRepository
    : IRepository<CustomFieldDefinition, CustomFieldDefinitionId>
{
    Task<IReadOnlyList<CustomFieldDefinition>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);
}

/// <summary>
/// Per-card, per-field values. Unique on <c>(FieldDefinitionId, CardId)</c>.
/// </summary>
public interface ICustomFieldValueRepository
    : IRepository<CustomFieldValue, CustomFieldValueId>
{
    Task<IReadOnlyList<CustomFieldValue>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default);

    Task<IReadOnlyList<CustomFieldValue>> ListForFieldAsync(
        CustomFieldDefinitionId fieldId, CancellationToken ct = default);

    Task<IReadOnlyList<CustomFieldValue>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);

    Task<CustomFieldValue?> GetByFieldAndCardAsync(
        CustomFieldDefinitionId fieldId, CardId cardId, CancellationToken ct = default);
}
