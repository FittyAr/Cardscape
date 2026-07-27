namespace Cardscape.Domain.Members;

/// <summary>Identifier of a user.</summary>
public sealed record UserId(Guid Value) : Common.GuidId<UserId>(Value);
