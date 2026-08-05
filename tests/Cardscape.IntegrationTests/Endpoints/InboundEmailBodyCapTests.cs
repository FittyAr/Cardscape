using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Coverage for the body cap the v1.2.0 audit (pass 6) added
/// to <c>POST /api/integrations/email/inbound</c>. The
/// previous incarnation read the request body straight to a
/// string with no upper bound; the 1 MB cap now blocks DoS
/// via huge unauthenticated POSTs.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class InboundEmailBodyCapTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public InboundEmailBodyCapTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Inbound_With_Oversized_Content_Length_Returns_413()
    {
        HttpClient client = _factory.CreateApiClient();

        // The cap is 1 MB; advertise 2 MB and the endpoint
        // short-circuits before allocating the read buffer.
        byte[] oversized = new byte[2 * 1024 * 1024];
        Array.Fill(oversized, (byte)'a');

        using HttpRequestMessage request = new(HttpMethod.Post, "api/integrations/email/inbound?provider=sendgrid")
        {
            Content = new ByteArrayContent(oversized)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Search_With_Oversized_Query_Returns_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        // The cap is 4 KB on the query string; a 6 KB query
        // is the smallest over-the-cap request we can
        // construct in a unit test without burning memory.
        string huge = new('x', 6 * 1024);

        HttpResponseMessage response = await client.GetAsync(
            $"api/search/?q={Uri.EscapeDataString(huge)}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"body-cap-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Body Cap Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
