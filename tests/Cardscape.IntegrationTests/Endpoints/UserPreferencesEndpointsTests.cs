using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.UserPreferences.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the per-user appearance
/// preferences endpoints added by
/// docs/roadmap/06-plan-radzen-themes.md commit 2:
///   GET  /api/users/me/preferences
///   POST /api/users/me/preferences
///   PUT  /api/users/me/preferences
/// All three require auth; the GET round-trips a
/// freshly-created row; the PUT validates the theme
/// name against the 12-entry catalogue.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class UserPreferencesEndpointsTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public UserPreferencesEndpointsTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_AsAnonymous_Returns401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("api/users/me/preferences", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_BeforeCreate_Returns200WithNullBody()
    {
        // A fresh user has no preferences row yet. The
        // endpoint is idempotent: GET returns 200 with a
        // null body, the client uses the null as the
        // signal to call POST. (A 404 would force the
        // client into a try/catch on every load — a
        // 200/null round-trip is friendlier and matches
        // the GetUserPreferencesQueryHandler's
        // Result<UserPreferencesDto?> shape.)
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage response = await client.GetAsync("api/users/me/preferences", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string? body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_CreatesRowWithDefaults()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage create = await client.PostAsync(
            "api/users/me/preferences", content: null, TestContext.Current.CancellationToken);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        UserPreferencesDto? created = await create.Content.ReadFromJsonAsync<UserPreferencesDto>(TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created!.ThemeName.Should().Be("default");
        created.Mode.Should().Be("System");
        created.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_IsIdempotent()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        // First POST creates the row.
        HttpResponseMessage first = await client.PostAsync(
            "api/users/me/preferences", content: null, TestContext.Current.CancellationToken);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        UserPreferencesDto? firstDto = await first.Content.ReadFromJsonAsync<UserPreferencesDto>(TestContext.Current.CancellationToken);

        // Second POST returns the existing row without
        // bumping CreatedAt — the safe-to-retry path the
        // Blazor client uses on the 404-then-POST flow.
        HttpResponseMessage second = await client.PostAsync(
            "api/users/me/preferences", content: null, TestContext.Current.CancellationToken);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        UserPreferencesDto? secondDto = await second.Content.ReadFromJsonAsync<UserPreferencesDto>(TestContext.Current.CancellationToken);
        secondDto!.CreatedAt.Should().Be(firstDto!.CreatedAt);
    }

    [Fact]
    public async Task Put_ValidThemeName_UpdatesRow()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create.
        await client.PostAsync("api/users/me/preferences", content: null, TestContext.Current.CancellationToken);

        // Put a custom theme.
        HttpResponseMessage put = await client.PutAsJsonAsync(
            "api/users/me/preferences",
            new { themeName = "cardscape-classic", mode = "Dark" },
            TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        UserPreferencesDto? updated = await put.Content.ReadFromJsonAsync<UserPreferencesDto>(TestContext.Current.CancellationToken);
        updated!.ThemeName.Should().Be("cardscape-classic");
        updated.Mode.Should().Be("Dark");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Put_InvalidThemeName_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        await client.PostAsync("api/users/me/preferences", content: null, TestContext.Current.CancellationToken);

        HttpResponseMessage put = await client.PutAsJsonAsync(
            "api/users/me/preferences",
            new { themeName = "not-a-real-theme" },
            TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_InvalidMode_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        await client.PostAsync("api/users/me/preferences", content: null, TestContext.Current.CancellationToken);

        HttpResponseMessage put = await client.PutAsJsonAsync(
            "api/users/me/preferences",
            new { mode = "NotAMode" },
            TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_AsAnonymous_Returns401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage put = await client.PutAsJsonAsync(
            "api/users/me/preferences",
            new { themeName = "humanistic" },
            TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_BeforeCreate_Returns404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        // No POST first — the row does not exist.
        HttpResponseMessage put = await client.PutAsJsonAsync(
            "api/users/me/preferences",
            new { themeName = "humanistic" },
            TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_AfterPut_ReturnsTheUpdatedRow()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        await client.PostAsync("api/users/me/preferences", content: null, TestContext.Current.CancellationToken);
        await client.PutAsJsonAsync(
            "api/users/me/preferences",
            new { themeName = "material-dark", mode = "Dark" },
            TestContext.Current.CancellationToken);

        // The GET /api/users/me/preferences returns 200
        // with the DTO after the PUT — the cookie-first
        // read path in the Blazor client only sees the
        // 404 case (fresh user, no row yet).
        HttpResponseMessage get = await client.GetAsync("api/users/me/preferences", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        UserPreferencesDto? dto = await get.Content.ReadFromJsonAsync<UserPreferencesDto>(TestContext.Current.CancellationToken);
        dto!.ThemeName.Should().Be("material-dark");
        dto.Mode.Should().Be("Dark");
    }

    [Fact]
    public async Task Get_AfterSoftDelete_Returns200WithNullBody()
    {
        // The UserPreferences row is dropped by the
        // SoftDeleteUserCommandHandler as part of the
        // GDPR cascade (docs/roadmap/06-plan-radzen-themes.md
        // commit 2). After a DSR self-delete, the GET
        // round-trips through 200/null again (same as
        // a fresh user) — the cascade is the same from
        // the API's perspective.
        HttpClient client = await CreateAuthenticatedClientAsync();
        string email = $"prefs-del-{Guid.NewGuid():N}@cardscape.local";
        await RegisterUserAsync(client, email);

        await client.PostAsync("api/users/me/preferences", content: null, TestContext.Current.CancellationToken);
        HttpResponseMessage beforeDelete = await client.GetAsync("api/users/me/preferences", TestContext.Current.CancellationToken);
        beforeDelete.StatusCode.Should().Be(HttpStatusCode.OK);

        // Soft-delete via the DSR self-delete endpoint.
        HttpResponseMessage dsr = await client.DeleteAsync("api/users/me/", TestContext.Current.CancellationToken);
        dsr.IsSuccessStatusCode.Should().BeTrue();

        // The preferences row should be gone too.
        HttpResponseMessage afterDelete = await client.GetAsync("api/users/me/preferences", TestContext.Current.CancellationToken);
        afterDelete.StatusCode.Should().Be(HttpStatusCode.OK);
        string? body = await afterDelete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"prefs-{Guid.NewGuid():N}@cardscape.local";
        await RegisterUserAsync(client, email);
        return client;
    }

    private static async Task RegisterUserAsync(HttpClient client, string email)
    {
        RegisterRequest register = new(email, "Prefs User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }
}
