using Cardscape.Api.Authentication;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class SamlMetadataHttpClientSecurityTests(CardscapeWebApplicationFactory factory)
{
    [Fact]
    public void SamlMetadataHttpClient_DisablesAutomaticRedirects()
    {
        var handlers = factory.Services.GetRequiredService<IHttpMessageHandlerFactory>();
        using HttpMessageHandler rootHandler = handlers.CreateHandler(
            SamlAuthenticationHandler.MetadataHttpClientName);
        HttpMessageHandler handler = rootHandler;
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        handler.Should().BeOfType<HttpClientHandler>()
            .Which.AllowAutoRedirect.Should().BeFalse();
    }
}
