using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class CustomFieldDefinitionRepository(CardscapeDbContext db)
    : RepositoryBase<CustomFieldDefinition, CustomFieldDefinitionId>(db),
        ICustomFieldDefinitionRepository
{
    public async Task<IReadOnlyList<CustomFieldDefinition>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        // AsAsyncEnumerable + client filter — the strongly-typed
        // BoardId.Value comparison can't be translated to SQL.
        var rows = new List<CustomFieldDefinition>();
        await foreach (CustomFieldDefinition d in Db.Set<CustomFieldDefinition>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (d.BoardId.Value == boardId.Value)
            {
                rows.Add(d);
            }
        }

        rows.Sort((a, b) => a.Position.CompareTo(b.Position));
        return rows;
    }
}

public sealed class CustomFieldValueRepository(CardscapeDbContext db)
    : RepositoryBase<CustomFieldValue, CustomFieldValueId>(db),
        ICustomFieldValueRepository
{
    public async Task<IReadOnlyList<CustomFieldValue>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        var rows = new List<CustomFieldValue>();
        await foreach (CustomFieldValue v in Db.Set<CustomFieldValue>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (v.CardId.Value == cardId.Value)
            {
                rows.Add(v);
            }
        }
        return rows;
    }

    public async Task<IReadOnlyList<CustomFieldValue>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        // Resolve which field ids belong to the board, then filter
        // values client-side. The repo can't join to definitions in a
        // single SQL query because both are accessed via AsAsyncEnumerable
        // (strongly-typed id filter on the .Value property). Bounded
        // by the number of fields in a board.
        var fieldIds = new HashSet<Guid>();
        await foreach (CustomFieldDefinition d in Db.Set<CustomFieldDefinition>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (d.BoardId.Value == boardId.Value)
            {
                fieldIds.Add(d.Id.Value);
            }
        }

        var rows = new List<CustomFieldValue>();
        await foreach (CustomFieldValue v in Db.Set<CustomFieldValue>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (fieldIds.Contains(v.FieldDefinitionId.Value))
            {
                rows.Add(v);
            }
        }
        return rows;
    }

    public async Task<CustomFieldValue?> GetByFieldAndCardAsync(
        CustomFieldDefinitionId fieldId,
        CardId cardId,
        CancellationToken ct = default)
    {
        await foreach (CustomFieldValue v in Db.Set<CustomFieldValue>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (v.FieldDefinitionId.Value == fieldId.Value && v.CardId.Value == cardId.Value)
            {
                return v;
            }
        }
        return null;
    }
}
