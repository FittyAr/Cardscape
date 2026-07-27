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
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("api/auth/register", register);
        registerResponse.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse? registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registered.Should().NotBeNull();
        registered!.User.Email.Should().Be(email);
        registered.AccessToken.Should().NotBeNullOrWhiteSpace();

        LoginRequest login = new(email, "Password123!");
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("api/auth/login", login);
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse? logged = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        logged.Should().NotBeNull();
        logged!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_With_Duplicate_Email_Returns_400()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"it-dup-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest first = new(email, "First", "Password123!");
        (await client.PostAsJsonAsync("api/auth/register", first)).IsSuccessStatusCode.Should().BeTrue();

        RegisterRequest second = new(email, "Second", "Password123!");
        HttpResponseMessage dup = await client.PostAsJsonAsync("api/auth/register", second);
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"it-bad-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest register = new(email, "Bad Pass", "Password123!");
        (await client.PostAsJsonAsync("api/auth/register", register)).IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage bad = await client.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(email, "wrong-password-1234"));
        bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Workspaces_Without_Token_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("api/workspaces/");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_Endpoint_Is_Public()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("health");
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
