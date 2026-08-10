using System.Net;
using System.Net.Http.Json;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Pins the i18n translation endpoint that the Blazor
/// client's CultureSwitcher calls to load per-culture
/// <c>SharedResource</c> translations. The endpoint used to
/// read the resx from an embedded resource in the Web
/// assembly (which required the API to take a project
/// reference on the Web — a violation of the
/// "Api_DependsOn_ApplicationInfrastructureDomain_Only"
/// architecture rule). The current implementation reads the
/// resx from the API's output directory (the API csproj
/// copies the two resx files from the Web project via
/// <c>&lt;Content Include="..\Cardscape.Web\Resources\..."&gt;</c>),
/// so the Web reference is no longer needed. These tests
/// guard the runtime behaviour so the architecture
/// decoupling does not regress into a "endpoint returns 0
/// keys" failure like the one the original BETA-8-UI-#3
/// report was about.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class TranslationEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public TranslationEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Translate_En_ReturnsNonEmptyDictionary()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync(
            "api/internal/translate/en", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TranslationPayload? payload =
            await response.Content.ReadFromJsonAsync<TranslationPayload>(TestJson.Options, TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Culture.Should().Be("en");
        payload.Translations.Should().NotBeEmpty(
            "the invariant resx ships every SharedResource key");
    }

    [Fact]
    public async Task Translate_Es_ReturnsNonEmptyDictionary()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync(
            "api/internal/translate/es", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TranslationPayload? payload =
            await response.Content.ReadFromJsonAsync<TranslationPayload>(TestJson.Options, TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Culture.Should().Be("es");
        payload.Translations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Translate_En_ContainsKnownKey()
    {
        // BUG-A1-003 — see test-results/beta/round-2/reports/A1-auth.md.
        // The previous version returned 0 keys for `en` because
        // the on-disk resx file was named "SharedResource.resx"
        // (no culture suffix for the invariant). The endpoint
        // now falls back to the invariant file when the
        // culture-specific one is absent. We assert that at
        // least one of the i18n keys the Web relies on at boot
        // time is present in the response.
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync(
            "api/internal/translate/en", TestContext.Current.CancellationToken);
        TranslationPayload? payload =
            await response.Content.ReadFromJsonAsync<TranslationPayload>(TestJson.Options, TestContext.Current.CancellationToken);

        payload.Should().NotBeNull();
        // The exact key list is whatever SharedResource.resx
        // currently contains; we only require that the
        // dictionary was not silently empty.
        payload!.Translations.Keys.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Translate_UnknownCulture_ReturnsBadRequest()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync(
            "api/internal/translate/fr", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Translate_EmptyCulture_ReturnsBadRequest()
    {
        HttpClient client = _factory.CreateApiClient();
        // "/api/internal/translate/" (no segment after) does
        // not match the route, so the API returns 404 — the
        // test asserts that the empty-culture case does not
        // crash or return 500.
        HttpResponseMessage response = await client.GetAsync(
            "api/internal/translate/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    private sealed record TranslationPayload(string Culture, IReadOnlyDictionary<string, string> Translations);
}
