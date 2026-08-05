using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;

namespace Cardscape.UnitTests.Domain.Aggregates;

public class WebhookEndpointTests
{
    private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static Result<WebhookEndpoint> TryCreate(string url) =>
        WebhookEndpoint.Create(
            WebhookEndpointId.New(),
            BoardId.New(),
            url,
            ValidHash,
            "card.created",
            DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("http://localhost/hook")]
    [InlineData("http://LOCALHOST:8080/hook")]
    [InlineData("http://api.localhost/hook")]
    [InlineData("http://intranet.local/hook")]
    [InlineData("http://api.internal/hook")]
    [InlineData("http://127.0.0.1/hook")]
    [InlineData("http://127.1.2.3/hook")]            // loopback, not 127.0.0.1
    [InlineData("http://10.0.0.5/hook")]             // RFC1918 10/8
    [InlineData("http://10.255.255.254/hook")]
    [InlineData("http://172.16.0.1/hook")]           // RFC1918 172.16/12
    [InlineData("http://172.31.255.254/hook")]
    [InlineData("http://192.168.1.1/hook")]          // RFC1918 192.168/16
    [InlineData("http://169.254.169.254/latest/meta-data/")] // AWS / GCP / Azure metadata
    [InlineData("http://0.0.0.0/hook")]              // unspecified
    [InlineData("http://224.0.0.1/hook")]            // IPv4 multicast
    [InlineData("http://[::1]/hook")]                // IPv6 loopback
    [InlineData("http://[fc00::1]/hook")]            // IPv6 unique-local
    [InlineData("http://[fe80::1]/hook")]            // IPv6 link-local
    [InlineData("http://[ff02::1]/hook")]            // IPv6 multicast
    public void Create_RejectsInternalOrLoopbackHosts(string url)
    {
        var result = TryCreate(url);

        result.IsFailure.Should().BeTrue(
            $"URL {url} should be rejected as internal/loopback, but Create succeeded.");
        result.Error.Code.Should().BeOneOf(
            "webhooks.url_host_internal",
            "webhooks.url_host_unresolvable");
    }

    [Theory]
    [InlineData("https://example.com/hook")]
    [InlineData("https://api.github.com/webhook")]
    [InlineData("https://hooks.slack.com/services/T00000000/B00000000/XXX")]
    public void Create_AcceptsPublicHosts(string url)
    {
        var result = TryCreate(url);

        result.IsSuccess.Should().BeTrue(
            $"URL {url} should be accepted, but Create returned: " +
            (result.IsFailure ? $"{result.Error.Code} {result.Error.Message}" : "n/a"));
    }

    [Fact]
    public void Create_RejectsNonHttpSchemes()
    {
        var result = TryCreate("file:///etc/passwd");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("webhooks.url_invalid");
    }

    [Fact]
    public void ChangeUrl_RejectsInternalHost()
    {
        var endpoint = TryCreate("https://example.com/hook").Value;

        var result = endpoint.ChangeUrl("http://192.168.1.1/admin");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("webhooks.url_host_internal");
        // The stored URL must NOT be replaced.
        endpoint.Url.Should().Be("https://example.com/hook");
    }
}
