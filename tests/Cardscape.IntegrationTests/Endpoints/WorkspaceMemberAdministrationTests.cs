using System.Net.Http.Headers;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Trello-style member administration: workspace Admins (not only
/// the owner) manage members and invitations, the owner stays
/// protected, instance admins can act on any workspace, and
/// invitation links onboard people into an invite-only instance —
/// by registering or from the inbox.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class WorkspaceMemberAdministrationTests(CardscapeWebApplicationFactory factory)
{
    internal const string Password = "Password123!";

    [Fact]
    public async Task WorkspaceAdmin_ManagesMembersAndInvitations_ButNotTheOwner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Admins manage");
        Account admin = await JoinAsync(owner.Client, ws.Id, "ws-admin", "admin");
        Account member = await JoinAsync(owner.Client, ws.Id, "ws-member", "member");

        // A plain member cannot manage.
        (await member.Client.GetAsync($"api/workspaces/{ws.Id}/invitations/", ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.PatchAsJsonAsync($"api/workspaces/{ws.Id}/members/{admin.Id}", new { role = "observer" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The workspace admin can invite, list, change roles and remove.
        HttpResponseMessage issue = await admin.Client.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/invitations/", new { email = NewEmail("x"), role = "member" }, ct);
        issue.StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.Client.GetAsync($"api/workspaces/{ws.Id}/invitations/", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.Client.PatchAsJsonAsync($"api/workspaces/{ws.Id}/members/{member.Id}", new { role = "observer" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await MembersAsync(owner.Client, ws.Id)).Single(m => m.UserId == member.Id).Role
            .Should().Be(WorkspaceRole.Observer);
        (await admin.Client.DeleteAsync($"api/workspaces/{ws.Id}/members/{member.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await MembersAsync(owner.Client, ws.Id)).Should().NotContain(m => m.UserId == member.Id);

        // The owner is protected from demotion and removal.
        (await admin.Client.PatchAsJsonAsync($"api/workspaces/{ws.Id}/members/{owner.Id}", new { role = "member" }, ct))
            .IsSuccessStatusCode.Should().BeFalse();
        (await admin.Client.DeleteAsync($"api/workspaces/{ws.Id}/members/{owner.Id}", ct))
            .IsSuccessStatusCode.Should().BeFalse();
        (await MembersAsync(owner.Client, ws.Id)).Single(m => m.UserId == owner.Id).Role
            .Should().Be(WorkspaceRole.Admin);
    }

    [Fact]
    public async Task InstanceAdmin_CanManageAWorkspaceTheyDoNotBelongTo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Admin override");
        Account member = await JoinAsync(owner.Client, ws.Id, "member", "member");
        Account instanceAdmin = await RegisterAsync(factory, "instance-admin");
        await factory.Services.PromoteUserToAdminAsync(instanceAdmin.Email, ct);

        (await instanceAdmin.Client.GetAsync($"api/workspaces/{ws.Id}/members", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await instanceAdmin.Client.PatchAsJsonAsync($"api/workspaces/{ws.Id}/members/{member.Id}", new { role = "admin" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvitedEmail_RegistersWhileRegistrationIsClosed_AndJoinsTheWorkspace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account owner = await RegisterAsync(host, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Invite only");
        await SetPublicRegistrationAsync(host, allow: false);

        string inviteeEmail = NewEmail("invitee");
        string token = await InviteAsync(owner.Client, ws.Id, inviteeEmail, "member");
        HttpClient anonymous = host.CreateClient();

        // Without a token the door is closed.
        (await anonymous.PostAsJsonAsync("api/auth/register", new RegisterRequest(NewEmail("walk-in"), "Walk In", Password), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The preview tells the accept page where to send the invitee.
        HttpResponseMessage preview = await anonymous.PostAsJsonAsync("api/invitations/preview", new { token }, ct);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await preview.Content.ReadAsStringAsync(ct)))
        {
            doc.RootElement.GetProperty("workspaceName").GetString().Should().Be("Invite only");
            doc.RootElement.GetProperty("email").GetString().Should().Be(inviteeEmail);
            doc.RootElement.GetProperty("accountExists").GetBoolean().Should().BeFalse();
        }

        // Someone else cannot spend the invitation on another address.
        (await anonymous.PostAsJsonAsync("api/auth/register", new RegisterRequest(NewEmail("thief"), "Thief", Password, token), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage register = await anonymous.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(inviteeEmail, "Invitee", Password, token), ct);
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        AuthResponse auth = (await register.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, ct))!;

        (await MembersAsync(owner.Client, ws.Id)).Should().Contain(m => m.UserId == auth.User.Id && m.Role == WorkspaceRole.Member);

        // The link is single-use.
        (await anonymous.PostAsJsonAsync("api/auth/register", new RegisterRequest(NewEmail("again"), "Again", Password, token), ct))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task InboxAccept_RequiresAVerifiedEmail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account owner = await RegisterAsync(host, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Inbox accept");
        Account invitee = await RegisterAsync(host, "invitee");
        Account stranger = await RegisterAsync(host, "stranger");
        await InviteAsync(owner.Client, ws.Id, invitee.Email, "observer");
        Guid invitationId = await PendingInvitationIdAsync(invitee.Client, ws.Id);

        // A self-registered address is unproven: the link (its token) is
        // still required.
        (await invitee.Client.PostAsync($"api/invitations/{invitationId}/accept", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await host.Services.MarkEmailVerifiedAsync(invitee.Email, ct);
        await host.Services.MarkEmailVerifiedAsync(stranger.Email, ct);
        (await stranger.Client.PostAsync($"api/invitations/{invitationId}/accept", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "the invitation is bound to another email");

        (await invitee.Client.PostAsync($"api/invitations/{invitationId}/accept", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await MembersAsync(owner.Client, ws.Id)).Should().Contain(m => m.UserId == invitee.Id && m.Role == WorkspaceRole.Observer);
    }

    [Fact]
    public async Task PublicRegistration_HonoursTheAllowedDomains_ButInvitationsBypassThem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        Account owner = await RegisterAsync(host, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Domains");
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            ISystemSettingsService settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            SystemSettings current = await settings.GetAsync(ct);
            current.Access.AllowedEmailDomains = "nexora.example";
            (await settings.UpdateAsync(current, "tests", ct)).IsSuccess.Should().BeTrue();
        }

        HttpClient anonymous = host.CreateClient();
        HttpResponseMessage outside = await anonymous.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(NewEmail("outsider"), "Outsider", Password), ct);
        outside.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outside.Content.ReadAsStringAsync(ct)).Should().Contain("Auth.EmailDomainNotAllowed");

        (await anonymous.PostAsJsonAsync(
                "api/auth/register", new RegisterRequest($"ada-{Guid.NewGuid():N}@nexora.example", "Ada", Password), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        string guestEmail = NewEmail("guest");
        string token = await InviteAsync(owner.Client, ws.Id, guestEmail, "member");
        (await anonymous.PostAsJsonAsync(
                "api/auth/register", new RegisterRequest(guestEmail, "Guest", Password, token), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created, "an explicit invitation is not limited by the domain list");
    }

    // ── helpers ────────────────────────────────────────────────

    internal sealed record Account(HttpClient Client, Guid Id, string Email);

    internal static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@cardscape.local";

    internal static async Task<Account> RegisterAsync(WebApplicationFactory<Program> host, string prefix)
    {
        HttpClient client = host.CreateClient();
        string email = NewEmail(prefix);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, prefix, Password), TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Account(client, auth.User.Id, email);
    }

    private async Task<Account> JoinAsync(HttpClient manager, Guid workspaceId, string prefix, string role)
    {
        Account account = await RegisterAsync(factory, prefix);
        string token = await InviteAsync(manager, workspaceId, account.Email, role);
        (await account.Client.PostAsJsonAsync("api/invitations/accept", new { token }, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();
        return account;
    }

    internal static async Task<string> InviteAsync(HttpClient manager, Guid workspaceId, string email, string role)
    {
        HttpResponseMessage response = await manager.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/invitations/", new { email, role }, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("cleartextToken").GetString()!;
    }

    private static async Task<Guid> PendingInvitationIdAsync(HttpClient invitee, Guid workspaceId)
    {
        using JsonDocument doc = JsonDocument.Parse(await invitee.GetStringAsync(
            "api/invitations/pending", TestContext.Current.CancellationToken));
        return doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("workspaceId").GetGuid() == workspaceId)
            .GetProperty("id").GetGuid();
    }

    internal static async Task<WorkspaceDto> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("api/workspaces/", new { name }, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    internal static async Task<IReadOnlyList<MemberRow>> MembersAsync(HttpClient client, Guid workspaceId) =>
        (await client.GetFromJsonAsync<MemberRow[]>($"api/workspaces/{workspaceId}/members", TestJson.Options, TestContext.Current.CancellationToken))!;

    internal sealed record MemberRow(Guid UserId, string Email, string DisplayName, WorkspaceRole Role, DateTimeOffset JoinedAt);

    internal static async Task SetPublicRegistrationAsync(WebApplicationFactory<Program> host, bool allow)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        ISystemSettingsService settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        SystemSettings current = await settings.GetAsync(TestContext.Current.CancellationToken);
        current.Access.AllowPublicRegistration = allow;
        (await settings.UpdateAsync(current, "tests", TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
    }
}
