using Cardscape.Application.Abstractions.Integrations;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class GoogleCalendarHttpClientSecurityTests(CardscapeWebApplicationFactory factory)
{
    [Theory]
    [InlineData("google-oauth")]
    [InlineData(nameof(IGoogleCalendarSyncService))]
    public void GoogleHttpClients_DisableAutomaticRedirects(string clientName)
    {
        var handlers = factory.Services.GetRequiredService<IHttpMessageHandlerFactory>();
        using HttpMessageHandler root = handlers.CreateHandler(clientName);
        HttpMessageHandler handler = root;
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        handler.Should().BeOfType<HttpClientHandler>()
            .Which.AllowAutoRedirect.Should().BeFalse();
    }
}
