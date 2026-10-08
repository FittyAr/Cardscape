using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Users.Queries;
using Cardscape.IntegrationTests.Fixtures;
using Cardscape.Tests.Common;
using Cardscape.Tests.Common.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// The instance Users page API: <c>GET /api/admin/users</c>,
/// deactivate / reactivate, and the guarantee that admin changes
/// and lock-outs apply to already-issued tokens on the next request.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class AdminUsersEndpointTests(CardscapeWebApplicationFactory factory)
{
    private const string Password = "Password123!";

    [Fact]
    public async Task List_RequiresAdmin()
    {
        (HttpClient member, _, _) = await RegisterAsync("users-list-member");

        HttpResponseMessage response = await member.GetAsync("api/admin/users", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_SearchesByEmailAndName_AndReportsState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient admin = await CreateAdminAsync();
        string marker = Guid.NewGuid().ToString("N")[..10];
        (_, Guid targetId, string targetEmail) = await RegisterAsync($"users-list-{marker}", $"Zoe {marker}");

        AdminUserPageDto byEmail = (await admin.GetFromJsonAsync<AdminUserPageDto>(
            $"api/admin/users?search={marker}", TestJson.Options, ct))!;
        byEmail.Total.Should().Be(1);
        AdminUserDto row = byEmail.Items.Should().ContainSingle().Subject;
        row.Id.Should().Be(targetId);
        row.Email.Should().Be(targetEmail);
        row.IsActive.Should().BeTrue();
        row.IsAdmin.Should().BeFalse();
        row.LastLoginAt.Should().NotBeNull("registering signs the user in");

        (await admin.PostAsync($"api/admin/users/{targetId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        AdminUserPageDto active = (await admin.GetFromJsonAsync<AdminUserPageDto>(
            $"api/admin/users?search={marker}&status=Active", TestJson.Options, ct))!;
        active.Total.Should().Be(0);
        AdminUserPageDto deactivated = (await admin.GetFromJsonAsync<AdminUserPageDto>(
            $"api/admin/users?search=zoe%20{marker}&status=Deactivated", TestJson.Options, ct))!;
        deactivated.Items.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_LocksOutExistingToken_AndReactivateRestoresIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient admin = await CreateAdminAsync();
        (HttpClient target, Guid targetId, string targetEmail) = await RegisterAsync("users-lockout");
        (await target.GetAsync("api/workspaces", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PostAsync($"api/admin/users/{targetId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await target.GetAsync("api/workspaces", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a deactivated user's still-valid JWT must stop working immediately");
        HttpResponseMessage login = await factory.CreateApiClient().PostAsJsonAsync(
            "api/auth/login", new LoginRequest(targetEmail, Password), ct);
        login.IsSuccessStatusCode.Should().BeFalse();

        (await admin.PostAsync($"api/admin/users/{targetId}/reactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await target.GetAsync("api/workspaces", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_Self_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient admin, Guid adminId) = await CreateAdminWithIdAsync();

        HttpResponseMessage response = await admin.PostAsync($"api/admin/users/{adminId}/deactivate", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RevokingAdmin_AppliesToTheExistingToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient first = await CreateAdminAsync();
        (HttpClient second, Guid secondId) = await CreateAdminWithIdAsync();
        (await second.GetAsync("api/admin/users", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await first.PostAsync($"api/admin/users/{secondId}/unadmin", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await second.GetAsync("api/admin/users", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the token still says is_admin=true, but the live lookup must win");
    }

    private async Task<HttpClient> CreateAdminAsync() => (await CreateAdminWithIdAsync()).Client;

    private async Task<(HttpClient Client, Guid UserId)> CreateAdminWithIdAsync()
    {
        (HttpClient client, Guid userId, string email) = await RegisterAsync("users-admin");
        await factory.Services.PromoteUserToAdminAsync(email, TestContext.Current.CancellationToken);
        return (client, userId);
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> RegisterAsync(
        string prefix, string displayName = "Test User")
    {
        HttpClient client = factory.CreateApiClient();
        string email = $"{prefix}-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, displayName, Password), TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User.Id, email);
    }
}
