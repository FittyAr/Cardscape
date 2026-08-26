using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CustomFieldDefinitionRepository(CardscapeDbContext db)
    : RepositoryBase<CustomFieldDefinition, CustomFieldDefinitionId>(db),
        ICustomFieldDefinitionRepository
{
    public async Task<IReadOnlyList<CustomFieldDefinition>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        return await Db.Set<CustomFieldDefinition>()
            .AsNoTracking()
            .Where(definition => definition.BoardId == boardId)
            .OrderBy(definition => definition.Position)
            .ToListAsync(ct);
    }
}

public sealed class CustomFieldValueRepository(CardscapeDbContext db)
    : RepositoryBase<CustomFieldValue, CustomFieldValueId>(db),
        ICustomFieldValueRepository
{
    public async Task<IReadOnlyList<CustomFieldValue>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        return await Db.Set<CustomFieldValue>()
            .AsNoTracking()
            .Where(value => value.CardId == cardId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomFieldValue>> ListForFieldAsync(
        CustomFieldDefinitionId fieldId, CancellationToken ct = default)
    {
        return await Db.Set<CustomFieldValue>()
            .Where(value => value.FieldDefinitionId == fieldId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomFieldValue>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        return await Db.Set<CustomFieldValue>()
            .Where(value => Db.Set<CustomFieldDefinition>().Any(definition =>
                definition.Id == value.FieldDefinitionId
                && definition.BoardId == boardId))
            .ToListAsync(ct);
    }

    public async Task<CustomFieldValue?> GetByFieldAndCardAsync(
        CustomFieldDefinitionId fieldId,
        CardId cardId,
        CancellationToken ct = default)
    {
        return await Db.Set<CustomFieldValue>().FirstOrDefaultAsync(value =>
            value.FieldDefinitionId == fieldId && value.CardId == cardId, ct);
    }
}
