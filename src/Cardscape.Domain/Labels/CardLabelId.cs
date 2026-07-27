namespace Cardscape.Domain.Labels;

/// <summary>Identifier of a card label join row.</summary>
public sealed record CardLabelId(Guid Value) : Common.GuidId<CardLabelId>(Value);
