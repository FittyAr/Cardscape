namespace Cardscape.Domain.Labels;

/// <summary>Identifier of a label.</summary>
public sealed record LabelId(Guid Value) : Common.GuidId<LabelId>(Value);
