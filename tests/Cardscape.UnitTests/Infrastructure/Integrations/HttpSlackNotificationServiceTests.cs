using System.Net;
using System.Text;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Integrations;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Infrastructure.Integrations;

public sealed class HttpSlackNotificationServiceTests
{
    [Fact]
    public async Task SendAsync_UsesCredentialOwnedByWorkspace()
    {
        const string botToken = "xoxb-workspace-owned-token";
        SlackWorkspace workspace = SlackWorkspace.Connect(
            SlackWorkspaceId.New(),
            new WorkspaceId(Guid.NewGuid()),
            "T-CARDSCAPE",
            "Cardscape",
            botToken,
            DateTimeOffset.UtcNow).Value;
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://slack.com/api/") };
        var service = new HttpSlackNotificationService(http, new IdentitySecretProtector());

        var result = await service.SendAsync(
            workspace, "C-CARDSCAPE", "hello", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        handler.Authorization.Should().Be($"Bearer {botToken}");
        handler.RequestUri.Should().Be(new Uri("https://slack.com/api/chat.postMessage"));
    }

    [Fact]
    public async Task SendAsync_WhenResponseExceedsLimit_ReturnsSafeExternalFailure()
    {
        SlackWorkspace workspace = SlackWorkspace.Connect(
            SlackWorkspaceId.New(),
            new WorkspaceId(Guid.NewGuid()),
            "T-CARDSCAPE",
            "Cardscape",
            "xoxb-secret",
            DateTimeOffset.UtcNow).Value;
        var handler = new OversizedResponseHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://slack.com/api/") };
        var service = new HttpSlackNotificationService(http, new IdentitySecretProtector());

        var result = await service.SendAsync(
            workspace, "C-CARDSCAPE", "hello", TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("slack.transport_error");
        result.Error.Message.Should().Be("Slack API call failed.");
        result.Error.Message.Should().NotContain("slack.com");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            });
        }
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
