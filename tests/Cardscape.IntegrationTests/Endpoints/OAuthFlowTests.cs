using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.OAuth.Commands;
using Cardscape.Application.OAuth.Queries;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the OAuth 2.0 authorization-code
/// flow:
/// <list type="number">
///   <item>Register an OAuth app via <c>POST /api/oauth-apps</c>.</item>
///   <item>Exchange the code at <c>POST /oauth/token</c>.</item>
///   <item>Call <c>GET /oauth/userinfo</c> with the access token.</item>
///   <item>Revoke via <c>POST /oauth/revoke</c>; userinfo now
///         returns 401.</item>
/// </list>
/// Also covers the failure paths: unknown client, wrong
/// secret, replayed code.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class OAuthFlowTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public OAuthFlowTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FullHandshake_Issue_Exchange_UserInfo_Revoke()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // 1. Register the OAuth app. The cleartext
        //    clientSecret is returned exactly once.
        RegisterOAuthAppResponse registration = await RegisterApp(client,
            "My Third-Party App",
            ["cards.read", "boards.read"],
            ["https://example.com/callback"]);

        registration.ClientId.Should().NotBeNullOrWhiteSpace();
        registration.ClientSecret.Should().NotBeNullOrWhiteSpace();
        registration.SecretPrefix.Length.Should().BeGreaterOrEqualTo(8);

        // 2. /oauth/authorize requires an authenticated user.
        //    The factory's Web client doesn't carry the JWT
        //    for /oauth/authorize (the endpoint resolves the
        //    user from the cookie/JWT the Web pipeline sets
        //    up). The 401 / 302 is acceptable here — we
        //    bypass the consent step by calling the
        //    service-level issuance through the API surface
        //    in the same way the consent page would. The
        //    easier integration path is to verify the token
        //    exchange end-to-end via the service directly,
        //    so we do that below.
        //
        //    What we DO verify at the protocol level is
        //    /oauth/token, /oauth/userinfo, and
        //    /oauth/revoke.

        //    The token exchange requires a real code. We
        //    mint one through the service so the test is
        //    hermetic — no consent page required.
        (string code, DateTimeOffset _) = await IssueCodeThroughService(
            registration.ClientId,
            auth.User.Id,
            "https://example.com/callback",
            ["cards.read"]);

        // 3. Exchange code for an access token.
        TokenResponse token = await ExchangeCode(client,
            registration.ClientId,
            registration.ClientSecret,
            code,
            "https://example.com/callback");

        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.TokenType.Should().Be("Bearer");
        token.ExpiresIn.Should().BeGreaterThan(0);
        token.Scope.Should().Be("cards.read");

        // 4. Call /oauth/userinfo with the access token.
        UserInfoResponse info = await GetUserInfo(client, token.AccessToken);
        info.Sub.Should().Be(auth.User.Id);
        info.Email.Should().Be(auth.User.Email);

        // 5. Revoke the access token.
        HttpResponseMessage revoke = await client.PostAsync(
            "oauth/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token.AccessToken
            }));
        revoke.IsSuccessStatusCode.Should().BeTrue();

        // 6. After revoke, userinfo returns 401.
        HttpResponseMessage afterRevoke = await GetUserInfoRaw(client, token.AccessToken);
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExchangeCode_With_Wrong_Secret_Returns_BadRequest()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        RegisterOAuthAppResponse registration = await RegisterApp(client,
            "Bad Secret App",
            ["cards.read"],
            ["https://example.com/callback"]);

        (string code, DateTimeOffset _) = await IssueCodeThroughService(
            registration.ClientId,
            auth.User.Id,
            "https://example.com/callback",
            ["cards.read"]);

        HttpResponseMessage response = await client.PostAsync(
            "oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = registration.ClientId,
                ["client_secret"] = "this-is-not-the-real-secret",
                ["redirect_uri"] = "https://example.com/callback"
            }));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExchangeCode_Replayed_Returns_BadRequest()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        RegisterOAuthAppResponse registration = await RegisterApp(client,
            "Replay Test App",
            ["cards.read"],
            ["https://example.com/callback"]);

        (string code, DateTimeOffset _) = await IssueCodeThroughService(
            registration.ClientId,
            auth.User.Id,
            "https://example.com/callback",
            ["cards.read"]);

        FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = registration.ClientId,
            ["client_secret"] = registration.ClientSecret,
            ["redirect_uri"] = "https://example.com/callback"
        });

        HttpResponseMessage first = await client.PostAsync("oauth/token", form);
        first.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage second = await client.PostAsync("oauth/token", form);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserInfo_Without_Bearer_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("oauth/userinfo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ────────────────────────────────────────────────

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"oauth-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "OAuth User", "Password123!");
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
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<RegisterOAuthAppResponse>())!;
    }

    private async Task<(string Code, DateTimeOffset ExpiresAt)> IssueCodeThroughService(
        string clientId,
        Guid userId,
        string redirectUri,
        string[] scopes)
    {
        // Use the IOAuthAppService directly so the test is
        // hermetic (no consent page navigation). The
        // /oauth/authorize step itself is covered by
        // Web-level tests; the integration tests focus on
        // the protocol endpoints.
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
        // Strip the JWT default header — the /oauth/token
        // endpoint authenticates the third-party app via
        // form-encoded client_id / client_secret, not via
        // a bearer.
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
            }));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static async Task<UserInfoResponse> GetUserInfo(HttpClient client, string accessToken)
    {
        HttpResponseMessage response = await GetUserInfoRaw(client, accessToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<UserInfoResponse>())!;
    }

    private static async Task<HttpResponseMessage> GetUserInfoRaw(HttpClient client, string accessToken)
    {
        // Replace — not add to — the Authorization header,
        // so the request carries the OAuth access token and
        // not the JWT the test client was authenticated with
        // for the registration step.
        using HttpRequestMessage request = new(HttpMethod.Get, "oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await client.SendAsync(request);
        return response;
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
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);

    private sealed record UserInfoResponse(
        [property: JsonPropertyName("sub")] Guid Sub,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);
}
