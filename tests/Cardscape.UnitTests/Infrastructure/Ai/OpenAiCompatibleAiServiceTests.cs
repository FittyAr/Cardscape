using System.Net;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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

    private static OpenAiCompatibleAiService CreateService(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://ai.example/") };
        return new OpenAiCompatibleAiService(
            http,
            Options.Create(new AiProviderOptions()),
            NullLogger<OpenAiCompatibleAiService>.Instance);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
