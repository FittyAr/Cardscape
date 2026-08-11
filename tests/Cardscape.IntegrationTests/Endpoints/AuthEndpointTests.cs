namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class AuthEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public AuthEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Then_Login_Returns_Token_For_Same_User()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"it-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest register = new(email, "Integration User", "Password123!");
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("api/auth/register", register, TestContext.Current.CancellationToken);
        registerResponse.IsSuccessStatusCode.Should().BeTrue();
        string registerJson = await registerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument registerDocument = JsonDocument.Parse(registerJson);
        registerDocument.RootElement.TryGetProperty("accessTokenExpiresAt", out _).Should().BeFalse();
        registerDocument.RootElement.TryGetProperty("refreshToken", out _).Should().BeFalse();
        registerDocument.RootElement.TryGetProperty("refreshTokenExpiresAt", out _).Should().BeFalse();
        AuthResponse? registered = JsonSerializer.Deserialize<AuthResponse>(registerJson, TestJson.Options);
        registered.Should().NotBeNull();
        registered!.User.Email.Should().Be(email);
        registered.AccessToken.Should().NotBeNullOrWhiteSpace();

        LoginRequest login = new(email, "Password123!");
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("api/auth/login", login, TestContext.Current.CancellationToken);
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
        string loginJson = await loginResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument loginDocument = JsonDocument.Parse(loginJson);
        loginDocument.RootElement.TryGetProperty("accessTokenExpiresAt", out _).Should().BeFalse();
        loginDocument.RootElement.TryGetProperty("refreshToken", out _).Should().BeFalse();
        loginDocument.RootElement.TryGetProperty("refreshTokenExpiresAt", out _).Should().BeFalse();
        AuthResponse? logged = JsonSerializer.Deserialize<AuthResponse>(loginJson, TestJson.Options);
        logged.Should().NotBeNull();
        logged!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_Route_Is_Not_Mapped()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/refresh",
            new { refreshToken = "unused", accessToken = "unsigned.jwt.payload" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_With_Duplicate_Email_Returns_400()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"it-dup-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest first = new(email, "First", "Password123!");
        (await client.PostAsJsonAsync("api/auth/register", first, TestContext.Current.CancellationToken)).IsSuccessStatusCode.Should().BeTrue();

        RegisterRequest second = new(email, "Second", "Password123!");
        HttpResponseMessage dup = await client.PostAsJsonAsync("api/auth/register", second, TestContext.Current.CancellationToken);
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"it-bad-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest register = new(email, "Bad Pass", "Password123!");
        (await client.PostAsJsonAsync("api/auth/register", register, TestContext.Current.CancellationToken)).IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage bad = await client.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(email, "wrong-password-1234"), TestContext.Current.CancellationToken);
        bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Workspaces_Without_Token_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_Endpoint_Is_Public()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("health", TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
