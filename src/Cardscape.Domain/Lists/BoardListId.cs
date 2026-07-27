namespace Cardscape.Domain.Lists;

/// <summary>Identifier of a list (column) on a board.</summary>
public sealed record BoardListId(Guid Value) : Common.GuidId<BoardListId>(Value);
