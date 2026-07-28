namespace Cardscape.Domain.Boards;

/// <summary>Identifier of a <see cref="CustomFieldDefinition"/>.</summary>
public sealed record CustomFieldDefinitionId(Guid Value) : Common.GuidId<CustomFieldDefinitionId>(Value);
