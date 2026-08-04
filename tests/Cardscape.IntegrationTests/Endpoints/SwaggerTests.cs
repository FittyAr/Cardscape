using System.Net;
using System.Text.Json;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Regression coverage for the OpenAPI document generation.
/// Endpoint body types are nested as <c>private record class
/// RenameBody</c> inside each endpoint class. Multiple endpoint
/// classes (Cards, Lists, …) define records with the same short
/// name, so the default Swashbuckle schemaId generator collides
/// and <c>GET /swagger/v1/swagger.json</c> returns 500. The fix
/// in <c>Program.cs</c> uses the full type name (with
/// <c>+</c> replaced by <c>.</c>) as the schemaId. This test
/// pins the contract so a future refactor cannot silently break
/// the docs.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class SwaggerTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public SwaggerTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Swagger_Document_Builds_And_Is_Served_As_Valid_Json()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync("swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotBeNullOrWhiteSpace();

        // Must be parseable JSON. If Swashbuckle blew up while
        // building the document the middleware would have
        // replaced the body with a Problem Details payload and
        // the parse would fail.
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("openapi", out JsonElement openapi).Should().BeTrue();
        openapi.GetString().Should().StartWith("3.");
        doc.RootElement.TryGetProperty("paths", out JsonElement paths).Should().BeTrue();
        paths.EnumerateObject().Should().NotBeEmpty("every endpoint should be documented");
    }
}
