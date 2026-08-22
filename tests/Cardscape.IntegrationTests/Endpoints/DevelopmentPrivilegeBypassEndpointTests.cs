using System.Net;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class DevelopmentPrivilegeBypassEndpointTests(
    CardscapeWebApplicationFactory factory)
{
    [Theory]
    [InlineData("api/dev/promote-self-admin")]
    [InlineData("api/dev/disable-totp")]
    public async Task PrivilegeBypassRoute_IsNotExposedInDevelopment(string route)
    {
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage response = await client.PostAsync(
            route, content: null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
