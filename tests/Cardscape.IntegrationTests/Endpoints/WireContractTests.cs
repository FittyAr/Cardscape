using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class WireContractTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public WireContractTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Search_Kind_Name_Is_Accepted_And_Numeric_Value_Is_Rejected()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage named = await client.GetAsync(
            "api/search/?q=contract&kind=card", TestContext.Current.CancellationToken);
        HttpResponseMessage numeric = await client.GetAsync(
            "api/search/?q=contract&kind=0", TestContext.Current.CancellationToken);

        named.StatusCode.Should().Be(HttpStatusCode.OK);
        numeric.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await numeric.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("search.kind_invalid");
    }

    [Fact]
    public async Task Numeric_Enum_In_Json_Body_Is_Rejected()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/boards/",
            new
            {
                workspaceId = Guid.NewGuid(),
                name = "Numeric enum must fail",
                description = (string?)null,
                visibility = 0
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register",
            new
            {
                email = $"wire-{Guid.NewGuid():N}@cardscape.local",
                displayName = "Wire Contract Test",
                password = "Goodpass123!"
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        string accessToken = document.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
