using Cardscape.Web.Services;

namespace Cardscape.UnitTests.Web;

public sealed class LocalReturnUrlTests
{
    [Theory]
    [InlineData("/boards/1")]
    [InlineData("/boards/1?tab=calendar#today")]
    [InlineData("boards/1")]
    public void Normalize_KeepsSameOriginPaths(string returnUrl) =>
        LocalReturnUrl.Normalize(returnUrl).Should().Be(returnUrl);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.example")]
    [InlineData("HTTP://evil.example/boards")]
    [InlineData("//evil.example")]
    [InlineData(@"/\evil.example")]
    [InlineData(@"\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("/\tboards")]
    public void Normalize_RejectsOffOriginTargets(string? returnUrl) =>
        LocalReturnUrl.Normalize(returnUrl).Should().Be("/");
}
