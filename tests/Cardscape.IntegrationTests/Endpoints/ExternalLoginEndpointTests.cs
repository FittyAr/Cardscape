using System.Net;
using System.Text.Json;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class ExternalLoginEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public ExternalLoginEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Start_UnknownProvider_Returns_CanonicalValidationProblem()
    {
        HttpClient client = _factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(
            "api/auth/external/unknown/start", TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            "Validation failed",
            "auth.external.unknown_provider");
    }

    [Fact]
    public async Task Start_UnconfiguredProvider_IsHiddenAsNotFound()
    {
        HttpClient client = _factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(
            "api/auth/external/google/start", TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "Resource not found",
            "auth.external.provider_unavailable");
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedCode)
    {
        response.StatusCode.Should().Be(expectedStatus);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        problem.RootElement.GetProperty("title").GetString().Should().Be(expectedTitle);
        problem.RootElement.GetProperty("code").GetString().Should().Be(expectedCode);
    }
}
