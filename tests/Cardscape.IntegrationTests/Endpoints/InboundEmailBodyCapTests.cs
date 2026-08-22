using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Coverage for the body cap + signature gate the v1.2.0 audit
/// (passes 6 + 7) added to
/// <c>POST /api/integrations/email/inbound</c>. The previous
/// incarnation was both unauthenticated and unbounded; this
/// test class pins both: the endpoint refuses to process any
/// request without a configured
/// <c>InboundEmail:SigningSecret</c>, validates the
/// HMAC-SHA256 signature header, and caps the body at 1 MB.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class InboundEmailBodyCapTests
{
    private const string TestSigningSecret = "inbound-email-test-signing-secret-please-rotate";

    private readonly CardscapeWebApplicationFactory _factory;
    public InboundEmailBodyCapTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Inbound_Without_Configured_Secret_Returns_503()
    {
        // The shared test factory does not set
        // InboundEmail:SigningSecret (the same pattern the
        // broadcast endpoint uses for Internal:Secret).
        // Without it, the endpoint short-circuits with 503
        // so a missing secret is loud, not silent.
        HttpClient client = _factory.CreateApiClient();
        using HttpRequestMessage request = new(HttpMethod.Post,
            "api/integrations/email/inbound?provider=sendgrid")
        {
            Content = JsonContent.Create(new { from = "x@example.com", subject = "s", text = "t" })
        };
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Inbound_With_Wrong_Signature_Returns_401()
    {
        HttpClient client = CreateClientWithSecret();

        using HttpRequestMessage request = new(HttpMethod.Post,
            "api/integrations/email/inbound?provider=sendgrid")
        {
            Content = JsonContent.Create(new { from = "x@example.com", subject = "s", text = "t" })
        };
        request.Headers.Add("X-Cardscape-Inbound-Signature", "deadbeef");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Inbound_With_Oversized_Content_Length_Returns_413()
    {
        HttpClient client = CreateClientWithSecret();

        byte[] oversized = new byte[2 * 1024 * 1024];
        Array.Fill(oversized, (byte)'a');

        using HttpRequestMessage request = new(HttpMethod.Post,
            "api/integrations/email/inbound?provider=sendgrid")
        {
            Content = new ByteArrayContent(oversized)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Cardscape-Inbound-Signature", "anything-non-empty");

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

    [Fact]
    public async Task Inbound_Without_Signature_Header_Returns_401_When_Secret_Is_Configured()
    {
        // With a configured secret, an unsigned request
        // must be rejected (401). The endpoint is the
        // primary attack surface for an unauthenticated
        // spam vector, so this pin is the most important
        // contract the v1.2.0 audit established.
        HttpClient client = CreateClientWithSecret();

        using HttpRequestMessage request = new(HttpMethod.Post,
            "api/integrations/email/inbound?provider=sendgrid")
        {
            Content = JsonContent.Create(new { from = "x@example.com", subject = "s", text = "t" })
        };
        // No X-Cardscape-Inbound-Signature header — the
        // endpoint must refuse the request before reading
        // the body.
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateClientWithSecret()
    {
        // Build a one-shot factory that has the
        // InboundEmail:SigningSecret configured. We
        // re-inject the parent factory's connection string
        // + storage root + provider so the per-test host
        // re-attaches the same physical database the rest
        // of the suite is using.
        WebApplicationFactory<Program> perTest = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["InboundEmail:SigningSecret"] = TestSigningSecret,
                    ["ConnectionStrings:Default"] = _factory.ConnectionString,
                    ["Storage:LocalRoot"] = _factory.StorageRoot,
                });
            });
        });
        return perTest.CreateClient();
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

    // ── seed helpers for the happy-path test ─────────────────

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var dto = (await resp.Content.ReadFromJsonAsync<WorkspaceDto>(TestContext.Current.CancellationToken))!;
        return dto.Id;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId, name, description = (string?)null, visibility = "private" });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var dto = (await resp.Content.ReadFromJsonAsync<BoardDto>(TestContext.Current.CancellationToken))!;
        return dto.Id;
    }

    private static async Task<Guid> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var dto = (await resp.Content.ReadFromJsonAsync<ListDto>(TestContext.Current.CancellationToken))!;
        return dto.Id;
    }

    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
}
