namespace Cardscape.Domain.Cards;

/// <summary>Identifier of a card member join row.</summary>
public sealed record CardMemberId(Guid Value) : Common.GuidId<CardMemberId>(Value);
