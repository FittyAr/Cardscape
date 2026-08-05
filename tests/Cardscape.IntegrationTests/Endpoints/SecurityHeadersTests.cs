using System.Net;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Coverage for the <c>SecurityHeadersMiddleware</c> the
/// v1.2.0 audit (pass 5) introduced. Every API response
/// (including 401s, 404s, and the unauthenticated /health
/// endpoint) must carry the project's security headers.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class SecurityHeadersTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public SecurityHeadersTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_Health_Returns_Security_Headers()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("health", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The middleware sets these on every response, even
        // an unauthenticated /health probe.
        response.Headers.Should().Contain(h => h.Key == "X-Content-Type-Options"
            && h.Value.Contains("nosniff"));
        response.Headers.Should().Contain(h => h.Key == "X-Frame-Options"
            && h.Value.Contains("DENY"));
        response.Headers.Should().Contain(h => h.Key == "Referrer-Policy"
            && h.Value.Contains("no-referrer"));
        response.Headers.Should().Contain(h => h.Key == "X-XSS-Protection"
            && h.Value.Contains("0"));
        response.Headers.Should().Contain(h => h.Key == "Cross-Origin-Opener-Policy"
            && h.Value.Contains("same-origin"));
        response.Headers.Should().Contain(h => h.Key == "Cross-Origin-Resource-Policy"
            && h.Value.Contains("same-origin"));
        response.Headers.Should().Contain(h => h.Key == "Permissions-Policy"
            && h.Value.Any(v => v.Contains("camera=()")));

        // HSTS is only emitted outside Development. The
        // test factory sets ASPNETCORE_ENVIRONMENT =
        // Development, so Strict-Transport-Security is
        // intentionally absent here. The middleware sets
        // it in non-Development — see the unit test in
        // SecurityHeadersMiddlewareTests.
        response.Headers.Should().NotContain(h => h.Key == "Strict-Transport-Security");
    }

    [Fact]
    public async Task Unauthorized_Search_Returns_Security_Headers()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("api/search?q=foo", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Security headers must land on the 401 too, so
        // an attacker probing the surface always sees the
        // hardened response shape.
        response.Headers.Should().Contain(h => h.Key == "X-Content-Type-Options"
            && h.Value.Contains("nosniff"));
        response.Headers.Should().Contain(h => h.Key == "X-Frame-Options"
            && h.Value.Contains("DENY"));
    }

    [Fact]
    public async Task Api_Responses_Carry_No_Store_Cache_Header()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("health", TestContext.Current.CancellationToken);

        // /health is not a static asset, so the middleware
        // should add Cache-Control: no-store. The default
        // for ASP.NET static files is much more permissive
        // (no Cache-Control on a controller-returned body);
        // adding no-store closes the "private data
        // accidentally cached on a shared proxy" hole.
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
