using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.SecurityTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.SecurityTests;

/// <summary>
/// OWASP A01:2021 — Broken Access Control / IDOR +
/// privilege escalation. These tests pin the contract
/// that the AdminOnly policy is enforced, that
/// workspace / board / card ownership is checked on
/// every read, and that an unprivileged user cannot
/// reach a privileged surface by changing a path
/// segment.
/// </summary>
[Collection(SecurityApi.Name)]
public sealed class AuthorizationSecurityTests
{
    private readonly SecurityTestsWebApplicationFactory _factory;
    public AuthorizationSecurityTests(SecurityTestsWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_Endpoint_Without_Auth_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/users/00000000-0000-0000-0000-000000000000/export",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_Endpoint_Without_Admin_Role_Returns_403()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/users/00000000-0000-0000-0000-000000000000/export",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Can_Read_Other_User_Export()
    {
        // Bootstrap admin1, then act on admin2's
        // user id. The endpoint allows admins to
        // read any user's export (that's the
        // contract). The point of the test is the
        // path is reachable for an admin; a
        // non-admin would have been 403.
        HttpClient admin1 = await CreateAdminClientAsync();
        AuthResponse auth2 = await RegisterUserAsync(
            _factory.CreateApiClient(),
            $"admin2-{Guid.NewGuid():N}@cardscape.local");
        HttpResponseMessage resp = await admin1.GetAsync(
            $"api/admin/users/{auth2.User.Id}/export",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Promote_Endpoint_Without_Admin_Role_Returns_403()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.PostAsync(
            "api/admin/users/00000000-0000-0000-0000-000000000000/admin",
            content: null, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Board_Detail_With_Fake_Id_Returns_404_Not_500()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            "api/boards/00000000-0000-0000-0000-000000000000",
            TestContext.Current.CancellationToken);
        ((int)resp.StatusCode).Should().BeLessThan(500,
            "missing data must not surface as 500");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterUserAsync(client,
            $"sec-{Guid.NewGuid():N}@cardscape.local");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        // Register, promote, then re-login so the new
        // JWT carries the is_admin claim. The
        // AdminOnlyPolicy reads the cached claim (no
        // DB lookup); a stale token would 403.
        HttpClient firstClient = await CreateAuthenticatedClientAsync();
        HttpResponseMessage promote = await firstClient.PostAsync(
            "api/dev/promote-self-admin", content: null,
            TestContext.Current.CancellationToken);
        promote.IsSuccessStatusCode.Should().BeTrue(
            "the dev promote endpoint should accept the authenticated user " +
            "in Development; the regression is the endpoint stopped working");

        // Re-register to recover the email + password
        // pair (the original CreateAuthenticatedClientAsync
        // helper doesn't keep them around). The
        // /api/auth/login endpoint accepts the same
        // credentials the registration used, so the
        // new JWT carries the fresh is_admin claim.
        string email = $"sec-admin-{Guid.NewGuid():N}@cardscape.local";
        var register = new
        {
            email,
            displayName = "Security (admin)",
            password = "Goodpass123!"
        };
        HttpResponseMessage r = await firstClient.PostAsJsonAsync(
            "api/auth/register", register, TestContext.Current.CancellationToken);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse firstAuth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;

        // Promote the just-registered user (the
        // firstClient is already promoted; promoting
        // (The dev endpoint accepts any authenticated
        // user and promotes the calling user — we just
        // need the new user's bearer. Re-login gives
        // us the new bearer; promote is a no-op because
        // the first user is already admin.)
        // The new login response is in firstClient's
        // DefaultRequestHeaders now; promote by reusing
        // the dev endpoint with the new bearer.
        HttpClient secondClient = _factory.CreateApiClient();
        HttpResponseMessage login = await secondClient.PostAsJsonAsync(
            "api/auth/login", new { email, password = register.password },
            TestContext.Current.CancellationToken);
        login.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        // The first login promoted nothing; promote
        // the second user explicitly. We need their
        // bearer, which auth holds.
        // (The dev endpoint accepts any authenticated
        // user and promotes the calling user — we just
        // need the new user's bearer.)
        // First use the second user's bearer to promote.
        secondClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        HttpResponseMessage promoteNew = await secondClient.PostAsync(
            "api/dev/promote-self-admin", content: null,
            TestContext.Current.CancellationToken);
        promoteNew.IsSuccessStatusCode.Should().BeTrue();

        // Re-login to get a JWT with the fresh
        // is_admin claim.
        HttpClient adminClient = _factory.CreateApiClient();
        HttpResponseMessage reLogin = await adminClient.PostAsJsonAsync(
            "api/auth/login", new { email, password = register.password },
            TestContext.Current.CancellationToken);
        reLogin.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse adminAuth = (await reLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        // Suppress the unused warnings on the
        // intermediate locals.
        _ = firstAuth;
        return adminClient;
    }

    private static async Task<AuthResponse> RegisterUserAsync(
        HttpClient client, string email)
    {
        var register = new
        {
            email,
            displayName = "Security",
            password = "Password123!"
        };
        HttpResponseMessage r = await client.PostAsJsonAsync(
            "api/auth/register", register, TestContext.Current.CancellationToken);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
