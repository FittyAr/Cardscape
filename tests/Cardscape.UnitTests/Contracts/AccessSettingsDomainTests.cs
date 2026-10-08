using Cardscape.Contracts.Settings;

namespace Cardscape.UnitTests.Contracts;

public sealed class AccessSettingsDomainTests
{
    [Theory]
    [InlineData("", "anyone@anything.test", true)]
    [InlineData("nexora.example", "ada@nexora.example", true)]
    [InlineData("nexora.example", "ada@EU.Nexora.Example", true)]
    [InlineData("nexora.example", "ada@notnexora.example", false)]
    [InlineData("nexora.example", "ada@gmail.com", false)]
    [InlineData("@nexora.example, partner.org", "bob@partner.org", true)]
    [InlineData("nexora.example\npartner.org", "bob@partner.org", true)]
    [InlineData("nexora.example", "not-an-email", false)]
    public void IsEmailDomainAllowed_MatchesListedDomainsAndTheirSubdomains(string domains, string email, bool expected)
    {
        AccessSettings access = new() { AllowedEmailDomains = domains };

        access.IsEmailDomainAllowed(email).Should().Be(expected);
    }

    [Fact]
    public void AllowedDomainList_NormalizesAndDropsJunk()
    {
        AccessSettings access = new() { AllowedEmailDomains = " @Nexora.Example ;nexora.example\r\nlocalhost, .partner.org " };

        access.AllowedDomainList().Should().Equal("nexora.example", "partner.org");
    }
}
