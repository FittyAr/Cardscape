namespace Cardscape.Domain.Integrations.OAuthApps;

public sealed record OAuthAppId(Guid Value) : Common.GuidId<OAuthAppId>(Value);
