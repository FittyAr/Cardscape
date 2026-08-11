using System.IdentityModel.Tokens.Jwt;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Security;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Extensions.Options;

namespace Cardscape.UnitTests.Security;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void IssueAccessToken_UsesConfiguredLifetimeForSignedExpiration()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 8, 11, 12, 30, 0, TimeSpan.Zero));
        var options = new JwtOptions
        {
            Issuer = "Cardscape.Tests",
            Audience = "Cardscape.Tests.Client",
            SigningKey = "unit-test-signing-key-with-at-least-32-bytes",
            AccessTokenMinutes = 17
        };
        var service = new JwtTokenService(Options.Create(options), clock);
        User user = BuildUser(clock.UtcNow);

        string serialized = service.IssueAccessToken(user, ["user"]);

        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(serialized);
        token.ValidFrom.Should().Be(clock.UtcNow.UtcDateTime);
        token.ValidTo.Should().Be(clock.UtcNow.AddMinutes(17).UtcDateTime);
        token.Issuer.Should().Be(options.Issuer);
        token.Audiences.Should().ContainSingle().Which.Should().Be(options.Audience);
    }

    private static User BuildUser(DateTimeOffset now) => User.Register(
        UserId.New(),
        EmailAddress.Create("jwt@example.com").Value,
        DisplayName.Create("JWT Test").Value,
        PasswordHash.FromHashed("v1.AAAA.BBBB").Value,
        now).Value;
}
