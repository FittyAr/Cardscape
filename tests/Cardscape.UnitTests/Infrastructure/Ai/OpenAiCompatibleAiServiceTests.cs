using System.Net;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Contracts.Settings;
using Cardscape.Infrastructure.Ai;
using Cardscape.Tests.Common.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Infrastructure.Ai;

public sealed class OpenAiCompatibleAiServiceTests
{
    [Fact]
    public async Task CompleteAsync_WithValidResponse_ReturnsCompletion()
    {
        const string json = """
            {"id":"answer-1","model":"llama3.2","choices":[{"index":0,"message":{"role":"assistant","content":"Ready"},"finish_reason":"stop"}],"usage":{"prompt_tokens":7,"completion_tokens":2,"total_tokens":9}}
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.CompleteAsync(
            new AiPrompt("system", "user"), new AiOptions(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new AiTextCompletion("Ready", "llama3.2", 7, 2));
        handler.RequestUri.Should().Be(new Uri("https://ai.example/v1/chat/completions"));
    }

    [Fact]
    public async Task CompleteAsync_WithOversizedResponse_ReturnsStableExternalError()
    {
        var handler = new StubHandler(HttpStatusCode.OK, new string('x', 1024 * 1024 + 1));
        var service = CreateService(handler);

        var result = await service.CompleteAsync(
            new AiPrompt("system", "user"), new AiOptions(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ai.response_too_large");
    }

    [Fact]
    public async Task CompleteAsync_WithInvalidJson_ReturnsStableExternalError()
    {
        var service = CreateService(new StubHandler(HttpStatusCode.OK, "not-json"));

        var result = await service.CompleteAsync(
            new AiPrompt("system", "user"), new AiOptions(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ai.invalid_response");
    }

    [Fact]
    public async Task CompleteAsync_WithProviderError_DoesNotExposeResponseBody()
    {
        const string secretBody = "provider-secret-must-not-escape";
        var service = CreateService(new StubHandler(HttpStatusCode.BadGateway, secretBody));

        var result = await service.CompleteAsync(
            new AiPrompt("system", "user"), new AiOptions(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ai.provider_error");
        result.Error.Message.Should().NotContain(secretBody);
    }

    [Fact]
    public async Task CompleteAsync_WhenDisabledByAdministrator_FailsWithoutCallingProvider()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler, ai => ai.Enabled = false);

        var result = await service.CompleteAsync(
            new AiPrompt("system", "user"), new AiOptions(), TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be("ai.disabled");
        handler.RequestUri.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_UsesStoredKeyAndCapsMaxTokens()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"a","model":"m","choices":[{"index":0,"message":{"role":"assistant","content":"ok"}}]}""");
        var service = CreateService(handler, ai => ai.MaxTokens = 100, apiKey: "sk-test");

        await service.CompleteAsync(new AiPrompt("system", "user"), new AiOptions(MaxTokens: 4000), TestContext.Current.CancellationToken);

        handler.Authorization.Should().Be("Bearer sk-test");
        handler.Body.Should().Contain("\"max_tokens\":100");
    }

    private static OpenAiCompatibleAiService CreateService(
        HttpMessageHandler handler, Action<AiSettings>? configure = null, string? apiKey = null)
    {
        var settings = new SystemSettings();
        settings.Ai.Endpoint = "https://ai.example/";
        configure?.Invoke(settings.Ai);
        return new OpenAiCompatibleAiService(
            new HttpClient(handler),
            new InMemorySystemSettingsService(settings, apiKey),
            NullLogger<OpenAiCompatibleAiService>.Instance);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? Authorization { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
