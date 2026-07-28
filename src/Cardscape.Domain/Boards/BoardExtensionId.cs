namespace Cardscape.Domain.Boards;

/// <summary>Identifier of a <see cref="BoardExtension"/>.</summary>
public sealed record BoardExtensionId(Guid Value) : Common.GuidId<BoardExtensionId>(Value);
