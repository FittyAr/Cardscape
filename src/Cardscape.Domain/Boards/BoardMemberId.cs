namespace Cardscape.Domain.Boards;

/// <summary>Identifier of a board member join row.</summary>
public sealed record BoardMemberId(Guid Value) : Common.GuidId<BoardMemberId>(Value);
