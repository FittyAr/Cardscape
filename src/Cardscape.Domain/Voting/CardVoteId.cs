namespace Cardscape.Domain.Voting;

/// <summary>Identifier of a per-card, per-user vote row.</summary>
public sealed record CardVoteId(Guid Value) : Common.GuidId<CardVoteId>(Value);
