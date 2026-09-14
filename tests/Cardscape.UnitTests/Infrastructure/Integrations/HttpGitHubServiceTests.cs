using System.Net;
using System.Text;
using Cardscape.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;

namespace Cardscape.UnitTests.Infrastructure.Integrations;

public sealed class HttpGitHubServiceTests
{
    [Fact]
    public async Task ListBranchesAsync_WhenResponseExceedsLimit_ReturnsSafeExternalFailure()
    {
        var handler = new OversizedResponseHandler();
        using var http = new HttpClient(handler);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Integrations:GitHub:Token"] = "test-token"
            })
            .Build();
        var service = new HttpGitHubService(http, configuration);

        var result = await service.ListBranchesAsync(
            "owner/repository", TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("github.transport_error");
        result.Error.Message.Should().Be("GitHub API call failed.");
        result.Error.Message.Should().NotContain("api.github.com");
    }

    private sealed class OversizedResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    new string('x', 1024 * 1024 + 1),
                    Encoding.UTF8,
                    "application/json")
            });
    }
}
