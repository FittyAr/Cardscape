namespace Cardscape.Domain.Authentication.ExternalLogins;

/// <summary>Identifier of an <see cref="ExternalLogin"/>.</summary>
public sealed record ExternalLoginId(Guid Value) : Common.GuidId<ExternalLoginId>(Value);
