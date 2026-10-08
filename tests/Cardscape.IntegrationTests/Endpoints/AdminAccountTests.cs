using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Tests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>Accounts created or reset by an administrator, and the forced password change.</summary>
public sealed class AdminAccountTests(CardscapeWebApplicationFactory factory)
    : IClassFixture<CardscapeWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatedUser_MustReplaceTheTemporaryPassword_BeforeUsingTheApi()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account admin = await RegisterAsync(host, "admin");
        await host.Services.PromoteUserToAdminAsync(admin.Email, Ct);

        string email = NewEmail("created");
        HttpResponseMessage create = await admin.Client.PostAsJsonAsync(
            "api/admin/users/", new { email, displayName = "Created Person", isAdmin = false }, Ct);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        string temporary;
        using (JsonDocument doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync(Ct)))
        {
            temporary = doc.RootElement.GetProperty("temporaryPassword").GetString()!;
        }

        temporary.Should().NotBeNullOrWhiteSpace("without SMTP the admin hands the password over");

        (HttpClient client, AuthResponse login) = await LoginAsync(host, email, temporary);
        login.MustChangePassword.Should().BeTrue();

        HttpResponseMessage blocked = await client.GetAsync("api/workspaces", Ct);
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        blocked.Headers.Contains("X-Cardscape-Password-Change").Should().BeTrue();

        (await client.PostAsJsonAsync("api/users/me/password",
                new { currentPassword = "wrong-password", newPassword = "Brand-New-Passw0rd" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        HttpResponseMessage changed = await client.PostAsJsonAsync("api/users/me/password",
            new { currentPassword = temporary, newPassword = "Brand-New-Passw0rd" }, Ct);
        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        // The same session keeps working: the flag is read per request.
        (await client.GetAsync("api/workspaces", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(host, email, "Brand-New-Passw0rd")).Login.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task AdminReset_GatesSessionsThatAreAlreadyOpen()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account admin = await RegisterAsync(host, "admin");
        await host.Services.PromoteUserToAdminAsync(admin.Email, Ct);
        Account user = await RegisterAsync(host, "user");
        (await user.Client.GetAsync("api/workspaces", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage reset = await admin.Client.PostAsJsonAsync(
            $"api/admin/users/{user.Id}/reset-password", new { }, Ct);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        (await user.Client.GetAsync("api/workspaces", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await host.CreateClient().PostAsJsonAsync("api/auth/login", new { email = user.Email, password = Password }, Ct))
            .IsSuccessStatusCode.Should().BeFalse("the old password no longer works");
    }

    [Fact]
    public async Task OnlyAdministrators_CreateOrResetAccounts()
    {
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account member = await RegisterAsync(host, "member");

        (await member.Client.PostAsJsonAsync("api/admin/users/", new { email = NewEmail("x"), displayName = "X" }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.PostAsJsonAsync($"api/admin/users/{member.Id}/reset-password", new { }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<(HttpClient Client, AuthResponse Login)> LoginAsync(
        WebApplicationFactory<Program> host, string email, string password)
    {
        HttpClient client = host.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("api/auth/login", new { email, password }, Ct);
        response.EnsureSuccessStatusCode();
        AuthResponse login = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return (client, login);
    }
}
