using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Members;

namespace Cardscape.UnitTests.Domain.Integrations;

/// <summary>
/// Expiry is judged against the caller's clock, never the wall clock,
/// so the dates here sit far from "now" in both directions.
/// </summary>
public sealed class OAuthAuthorizationCodeTests
{
    [Fact]
    public void MarkConsumed_BeforeExpiry_Succeeds()
    {
        DateTimeOffset issuedAt = new(2001, 1, 1, 12, 0, 0, TimeSpan.Zero);
        OAuthAuthorizationCode code = Issue(issuedAt, expiresAt: issuedAt.AddMinutes(10));

        code.MarkConsumed(issuedAt.AddMinutes(5)).IsSuccess.Should().BeTrue();
        code.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public void MarkConsumed_AtOrAfterExpiry_Fails()
    {
        DateTimeOffset issuedAt = new(2099, 1, 1, 12, 0, 0, TimeSpan.Zero);
        OAuthAuthorizationCode code = Issue(issuedAt, expiresAt: issuedAt.AddMinutes(10));

        var result = code.MarkConsumed(issuedAt.AddMinutes(10));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("oauth.code_expired");
        code.IsConsumed.Should().BeFalse();
    }

    private static OAuthAuthorizationCode Issue(DateTimeOffset at, DateTimeOffset expiresAt) =>
        OAuthAuthorizationCode.Issue(
            new OAuthAuthorizationCodeId(Guid.NewGuid()),
            new OAuthAppId(Guid.NewGuid()),
            new UserId(Guid.NewGuid()),
            "https://example.test/callback",
            "code-hash",
            ["read"],
            expiresAt,
            at).Value;
}
