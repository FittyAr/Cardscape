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
    string ValueJson,
    string FieldName,
    int Position)
{
    public static CustomFieldValueDto FromEntity(CustomFieldValue value, CustomFieldDefinition field) =>
        new(field.Id.Value, value.CardId.Value, field.Kind, value.ValueJson, field.Name, field.Position);

    /// <summary>The field shown with no value (a cleared value).</summary>
    public static CustomFieldValueDto Empty(CustomFieldDefinition field, Guid cardId) =>
        new(field.Id.Value, cardId, field.Kind, string.Empty, field.Name, field.Position);
}
