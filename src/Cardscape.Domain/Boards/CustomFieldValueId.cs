namespace Cardscape.Domain.Boards;

/// <summary>Identifier of a <see cref="CustomFieldValue"/>.</summary>
public sealed record CustomFieldValueId(Guid Value) : Common.GuidId<CustomFieldValueId>(Value);
