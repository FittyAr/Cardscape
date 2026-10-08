using Cardscape.Api.Endpoints.Integrations;

namespace Cardscape.UnitTests.Api.Integrations;

public sealed class InboundEmailProviderResolutionTests
{
    [Theory]
    [InlineData("Mailgun", "postmark", "mailgun")]
    [InlineData("", "Postmark", "postmark")]
    [InlineData(null, "postmark", "postmark")]
    [InlineData("  ", null, "sendgrid")]
    [InlineData(null, "", "sendgrid")]
    public void ResolveInboundEmailProvider_FallsBackFromQueryToHeaderToSendGrid(
        string? queryValue, string? headerValue, string expected) =>
        IntegrationsEndpoints.ResolveInboundEmailProvider(queryValue, headerValue).Should().Be(expected);
}
