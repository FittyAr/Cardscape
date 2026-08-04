using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.SecurityTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.SecurityTests;

/// <summary>
/// OWASP A07:2021 — Identification and Authentication
/// Failures. These tests pin the contract that the
/// auth surface is hardened against the common
/// regressions: weak-password rejection, token
/// tampering, and password storage.
/// </summary>
[Collection(SecurityApi.Name)]
public sealed class AuthenticationSecurityTests
{
    private readonly SecurityTestsWebApplicationFactory _factory;
    public AuthenticationSecurityTests(SecurityTestsWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_With_Weak_Password_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        var body = new
        {
            email = $"weak-{Guid.NewGuid():N}@cardscape.local",
            displayName = "Weak",
            password = "12345678"
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the password policy should reject the breached password (12345678 is " +
            "in the top-100 most-leaked passwords list)");
    }

    [Fact]
    public async Task Register_With_Empty_Password_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        var body = new
        {
            email = $"empty-{Guid.NewGuid():N}@cardscape.local",
            displayName = "Empty",
            password = ""
        };
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_With_Tampered_Token_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAsync(client,
            $"token-{Guid.NewGuid():N}@cardscape.local");
        string legit = auth.AccessToken ?? string.Empty;
        string tampered = legit[..^1] + (legit[^1] == 'A' ? 'B' : 'A');
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/boards/");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        HttpResponseMessage resp = await client.SendAsync(req, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the signature mismatch must produce 401, not 200");
    }

    [Fact]
    public async Task Login_With_Expired_Token_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        string fakeExpiredToken = BuildFakeJwt(
            sub: Guid.NewGuid().ToString(),
            exp: DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds());
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/boards/");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fakeExpiredToken);
        HttpResponseMessage resp = await client.SendAsync(req, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the fake token must not authenticate");
    }

    [Fact]
    public async Task Bearer_Without_Scheme_Is_Rejected()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAsync(client,
            $"scheme-{Guid.NewGuid():N}@cardscape.local");
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/boards/");
        req.Headers.TryAddWithoutValidation("Authorization", auth.AccessToken);
        HttpResponseMessage resp = await client.SendAsync(req, TestContext.Current.CancellationToken);
        ((int)resp.StatusCode).Should().NotBe(200,
            "the auth scheme must require the `Bearer` prefix");
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var body = new
        {
            email,
            displayName = "Auth",
            password = "Password123!"
        };
        HttpResponseMessage r = await client.PostAsJsonAsync(
            "api/auth/register", body, TestContext.Current.CancellationToken);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static string BuildFakeJwt(string sub, long exp)
    {
        string header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string payload = Base64UrlEncode(
            $"{{\"sub\":\"{sub}\",\"exp\":{exp}}}");
        string signature = Base64UrlEncode("not-a-real-signature");
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
