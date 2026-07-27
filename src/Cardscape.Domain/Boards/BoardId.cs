namespace Cardscape.Domain.Boards;

/// <summary>Identifier of a board.</summary>
public sealed record BoardId(Guid Value) : Common.GuidId<BoardId>(Value);
