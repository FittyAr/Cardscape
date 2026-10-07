using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid BoardId,
    string Name,
    CustomFieldKind Kind,
    string OptionsJson,
    int Position)
{
    public static CustomFieldDefinitionDto FromEntity(CustomFieldDefinition d) => new(
        d.Id.Value, d.BoardId.Value, d.Name, d.Kind, d.OptionsJson, d.Position);
}

public sealed record CustomFieldValueDto(
    Guid FieldDefinitionId,
    Guid CardId,
    CustomFieldKind Kind,
    string ValueJson)
{
    public static CustomFieldValueDto FromEntity(CustomFieldValue v, CustomFieldKind kind) =>
        new(v.FieldDefinitionId.Value, v.CardId.Value, kind, v.ValueJson);
}
