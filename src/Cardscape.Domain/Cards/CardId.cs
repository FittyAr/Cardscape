namespace Cardscape.Domain.Cards;

/// <summary>Identifier of a card.</summary>
public sealed record CardId(Guid Value) : Common.GuidId<CardId>(Value);
