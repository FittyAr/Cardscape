using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Infrastructure.DependencyInjection;

public static class HttpClientBuilderExtensions
{
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Uses a primary handler that never follows redirects. Every client
        /// that calls an address a user or an integration controls needs this:
        /// a redirect could otherwise bounce the request to an internal host
        /// after the URL was validated.
        /// </summary>
        public IHttpClientBuilder WithoutAutoRedirect() =>
            builder.ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler { AllowAutoRedirect = false });
    }
}
