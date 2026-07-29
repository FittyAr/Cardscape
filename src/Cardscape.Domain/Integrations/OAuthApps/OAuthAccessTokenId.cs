namespace Cardscape.Domain.Integrations.OAuthApps;

public sealed record OAuthAccessTokenId(Guid Value)
    : Common.GuidId<OAuthAccessTokenId>(Value);
