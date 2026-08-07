using System.Net;
using System.Text.Json;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Regression coverage for the OpenAPI document generation.
/// The document is produced by the native .NET 10+
/// <c>Microsoft.AspNetCore.OpenApi</c> generator (added via
/// <c>AddOpenApi()</c> in <c>Program.cs</c>) and served at
/// <c>GET /openapi/v1.json</c> in the Development environment.
/// The Scalar reference UI (<c>/scalar</c>) renders on top of
/// that same document. This test pins the contract so a future
/// refactor cannot silently break the public API surface that
/// SDK generators, the MCP server, and third-party consumers
/// read from.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class OpenApiTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public OpenApiTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApi_Document_Builds_And_Is_Served_As_Valid_Json()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync("openapi/v1.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotBeNullOrWhiteSpace();

        // Must be parseable JSON. If the generator blew up while
        // building the document the middleware would have
        // replaced the body with a Problem Details payload and
        // the parse would fail.
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("openapi", out JsonElement openapi).Should().BeTrue();
        openapi.GetString().Should().StartWith("3.");
        doc.RootElement.TryGetProperty("paths", out JsonElement paths).Should().BeTrue();
        paths.EnumerateObject().Should().NotBeEmpty("every endpoint should be documented");
    }

    [Fact]
    public async Task OpenApi_Document_Exposes_Bearer_Security_Scheme()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync("openapi/v1.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty("components", out JsonElement components).Should().BeTrue();
        components.TryGetProperty("securitySchemes", out JsonElement schemes).Should().BeTrue();
        schemes.TryGetProperty("Bearer", out JsonElement bearer).Should().BeTrue(
            "the Bearer scheme is contributed by BearerSecuritySchemeTransformer so Scalar renders the Authorize button");
        bearer.GetProperty("type").GetString().Should().Be("http");
        bearer.GetProperty("scheme").GetString().Should().Be("bearer");
        bearer.GetProperty("bearerFormat").GetString().Should().Be("JWT");
    }
}
