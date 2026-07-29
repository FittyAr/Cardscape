namespace Cardscape.Domain.Authentication.Totp;

/// <summary>Identifier of a <see cref="TotpCredential"/>.</summary>
public sealed record TotpCredentialId(Guid Value) : Common.GuidId<TotpCredentialId>(Value);
