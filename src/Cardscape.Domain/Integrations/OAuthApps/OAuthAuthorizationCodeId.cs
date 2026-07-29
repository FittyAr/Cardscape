namespace Cardscape.Domain.Integrations.OAuthApps;

public sealed record OAuthAuthorizationCodeId(Guid Value)
    : Common.GuidId<OAuthAuthorizationCodeId>(Value);
