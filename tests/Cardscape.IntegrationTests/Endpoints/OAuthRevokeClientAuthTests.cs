using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Coverage for the RFC 7009 /oauth/revoke behaviour the
/// v1.2.0 audit (pass 5) introduced: the endpoint now
/// requires the calling client to authenticate with its own
/// <c>client_id</c> + <c>client_secret</c> and refuses to
/// revoke a token owned by a different client. A bad or
/// missing presentation, a wrong secret, and a cross-client
/// revoke are all rejected.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class OAuthRevokeClientAuthTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public OAuthRevokeClientAuthTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Revoke_Without_ClientCredentials_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        _ = auth;

        HttpResponseMessage response = await client.PostAsync(
            "oauth/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = "any-token"
            }), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoke_With_WrongClientSecret_Returns_400()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        RegisterOAuthAppResponse registration = await RegisterApp(client,
            "Wrong Secret App",
            ["cards.read"],
            ["https://example.com/callback"]);

        (string code, _) = await IssueCodeThroughService(registration.ClientId,
            auth.User.Id, "https://example.com/callback", ["cards.read"]);
        TokenResponse token = await ExchangeCode(client, registration.ClientId,
            registration.ClientSecret, code, "https://example.com/callback");

        // Restore the JWT since ExchangeCode nulled it.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage response = await client.PostAsync(
            "oauth/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token.AccessToken,
                ["client_id"] = registration.ClientId,
                ["client_secret"] = "this-is-not-the-real-secret"
            }), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The token is still usable after a failed revoke —
        // /oauth/userinfo returns 200 with the original
        // principal, not 401. Otherwise a wrong-secret
        // presentation would silently disable the token.
        HttpResponseMessage userInfo = await GetUserInfo(client, token.AccessToken);
        userInfo.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoke_With_OtherClientsCredentials_Returns_400_AndToken_Stays()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);

        // First app: owns the token. Use the JWT for the
        // management endpoint, then stash it for the second
        // registration later (ExchangeCode clears the
        // Authorization header, so we have to restore it).
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        RegisterOAuthAppResponse owner = await RegisterApp(client,
            "Owner App",
            ["cards.read"],
            ["https://example.com/callback"]);

        (string code, _) = await IssueCodeThroughService(owner.ClientId,
            auth.User.Id, "https://example.com/callback", ["cards.read"]);
        TokenResponse token = await ExchangeCode(client, owner.ClientId,
            owner.ClientSecret, code, "https://example.com/callback");

        // Restore the JWT for the second registration.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Second app: a different third-party that wants to
        // revoke the owner's token. RFC 7009 says this is
        // an invalid_client; the server must reject the
        // revoke and the token must remain valid.
        RegisterOAuthAppResponse attacker = await RegisterApp(client,
            "Attacker App",
            ["cards.read"],
            ["https://example.com/callback"]);

        HttpResponseMessage response = await client.PostAsync(
            "oauth/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token.AccessToken,
                ["client_id"] = attacker.ClientId,
                ["client_secret"] = attacker.ClientSecret
            }), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage userInfo = await GetUserInfo(client, token.AccessToken);
        userInfo.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoke_Accepts_HttpBasic_ClientCredentials()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        RegisterOAuthAppResponse registration = await RegisterApp(client,
            "Basic Auth App",
            ["cards.read"],
            ["https://example.com/callback"]);

        (string code, _) = await IssueCodeThroughService(registration.ClientId,
            auth.User.Id, "https://example.com/callback", ["cards.read"]);
        TokenResponse token = await ExchangeCode(client, registration.ClientId,
            registration.ClientSecret, code, "https://example.com/callback");

        // The form-params branch works (covered by the
        // happy-path test in OAuthFlowTests). This one
        // exercises the HTTP Basic auth branch that
        // RFC 7009 §2.1 recommends for confidential
        // clients.
        string basic = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{registration.ClientId}:{registration.ClientSecret}"));
        using HttpRequestMessage request = new(HttpMethod.Post, "oauth/revoke")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token.AccessToken
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage userInfo = await GetUserInfo(client, token.AccessToken);
        userInfo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ────────────────────────────────────────────────

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"oauth-revoke-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "OAuth Revoke User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<RegisterOAuthAppResponse> RegisterApp(
        HttpClient client, string name, string[] scopes, string[] redirectUris)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/oauth-apps",
            new { name, allowedScopes = scopes, redirectUris });
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"RegisterApp '{name}' failed: {(int)response.StatusCode} {response.StatusCode} body={body}");
        }
        return (await response.Content.ReadFromJsonAsync<RegisterOAuthAppResponse>())!;
    }

    private async Task<(string Code, DateTimeOffset ExpiresAt)> IssueCodeThroughService(
        string clientId, Guid userId, string redirectUri, string[] scopes)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IOAuthAppService service = scope.ServiceProvider
            .GetRequiredService<IOAuthAppService>();
        OAuthAuthorizationCodeIssuance issuance = await service.IssueAuthorizationCodeAsync(
            clientId,
            new Domain.Members.UserId(userId),
            redirectUri,
            scopes,
            CancellationToken.None);
        return (issuance.Code, issuance.ExpiresAt);
    }

    private static async Task<TokenResponse> ExchangeCode(
        HttpClient client, string clientId, string clientSecret, string code, string redirectUri)
    {
        client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client.PostAsync(
            "oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri
            }), TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static async Task<HttpResponseMessage> GetUserInfo(
        HttpClient client, string accessToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record RegisterOAuthAppResponse(
        Guid Id,
        string ClientId,
        string ClientSecret,
        string SecretPrefix);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string Scope);
}
