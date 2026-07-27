namespace Cardscape.Domain.Security;

/// <summary>Identifier of an <see cref="ApiToken"/>.</summary>
public sealed record ApiTokenId(Guid Value) : Common.GuidId<ApiTokenId>(Value);
