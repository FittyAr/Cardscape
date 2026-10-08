using System.Text.RegularExpressions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Tests.Common.Fakes;
using Cardscape.Tests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Outbound email end to end, with a recording <see cref="IEmailSender"/>
/// in place of SMTP: invitations are emailed only when SMTP is configured in
/// System settings, the link is returned either way, the SMTP password is
/// write-only, the admin test button sends to the admin, and password-reset
/// links arrive by email.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed partial class InvitationEmailTests(CardscapeWebApplicationFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invitation_WithoutSmtp_ReturnsTheLink_AndSendsNothing()
    {
        (WebApplicationFactory<Program> host, RecordingEmailSender sender) = CreateHost();
        using (host)
        {
            Account owner = await RegisterAsync(host, "owner");
            WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "No SMTP");

            Issued issued = await IssueAsync(owner.Client, ws.Id, NewEmail("guest"));

            issued.EmailStatus.Should().Be("notConfigured");
            issued.AcceptUrl.Should().Be(
                $"http://localhost/invitations/accept?token={Uri.EscapeDataString(issued.CleartextToken)}");
            sender.Sent.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Invitation_WithSmtp_EmailsTheInvitee_WithTheLinkThatJoinsTheWorkspace()
    {
        (WebApplicationFactory<Program> host, RecordingEmailSender sender) = CreateHost();
        using (host)
        {
            await ConfigureSmtpAsync(host, publicBaseUrl: "https://boards.example.test/");
            Account owner = await RegisterAsync(host, "owner");
            WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Launch team");
            Account invitee = await RegisterAsync(host, "invitee");
            sender.Clear(); // the sign-ups' verification emails

            Issued issued = await IssueAsync(owner.Client, ws.Id, invitee.Email, language: "en");

            issued.EmailStatus.Should().Be("sent");
            issued.AcceptUrl.Should().StartWith("https://boards.example.test/invitations/accept?token=");
            OutboundEmail email = sender.Sent.Should().ContainSingle().Subject;
            email.To.Should().Be(invitee.Email);
            email.Subject.Should().Be("owner invited you to \"Launch team\" on Email Tests");
            email.TextBody.Should().Contain(issued.AcceptUrl!);

            // The emailed link carries a working token.
            string token = Uri.UnescapeDataString(TokenInLink().Match(email.TextBody).Groups[1].Value);
            (await invitee.Client.PostAsJsonAsync("api/invitations/accept", new { token }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Invitation_WhenSmtpRejectsTheEmail_IsStillCreated_AndReportsTheFailure()
    {
        (WebApplicationFactory<Program> host, RecordingEmailSender sender) = CreateHost();
        using (host)
        {
            await ConfigureSmtpAsync(host);
            sender.Fail = true;
            Account owner = await RegisterAsync(host, "owner");
            WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Bounce");
            string guest = NewEmail("guest");

            Issued issued = await IssueAsync(owner.Client, ws.Id, guest);

            issued.EmailStatus.Should().Be("failed");
            issued.AcceptUrl.Should().NotBeNull();
            (await host.CreateClient().PostAsJsonAsync("api/invitations/preview", new { token = issued.CleartextToken }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.OK, "the link still works when the email does not go out");
            string invitations = await owner.Client.GetStringAsync($"api/workspaces/{ws.Id}/invitations/", Ct);
            invitations.Should().Contain(guest);
        }
    }

    [Fact]
    public async Task AdminSettings_StoreTheSmtpPasswordWriteOnly_AndTheTestEmailGoesToTheAdmin()
    {
        (WebApplicationFactory<Program> host, RecordingEmailSender sender) = CreateHost();
        using (host)
        {
            Account admin = await RegisterAsync(host, "admin");
            await host.Services.PromoteUserToAdminAsync(admin.Email, Ct);

            // Before SMTP is configured the test reports why nothing was sent.
            EmailTestResult notConfigured = await TestEmailAsync(admin.Client);
            notConfigured.Success.Should().BeFalse();
            sender.Sent.Should().BeEmpty();

            SystemSettings settings = (await admin.Client.GetFromJsonAsync<SystemSettings>("api/admin/settings", TestJson.Options, Ct))!;
            settings.Email = TestEmailSettings.Configured();
            settings.Email.Username = "mailer";
            settings.Email.Password = "hunter2";
            HttpResponseMessage put = await admin.Client.PutAsJsonAsync("api/admin/settings", settings, TestJson.Options, Ct);
            put.StatusCode.Should().Be(HttpStatusCode.OK);

            string raw = await admin.Client.GetStringAsync("api/admin/settings", Ct);
            raw.Should().NotContain("hunter2");
            SystemSettings saved = JsonSerializer.Deserialize<SystemSettings>(raw, TestJson.Options)!;
            saved.Email.HasPassword.Should().BeTrue();
            saved.Email.Password.Should().BeNull();
            saved.Email.Host.Should().Be("smtp.example.test");
            await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
            {
                (await scope.ServiceProvider.GetRequiredService<ISystemSettingsService>().GetSmtpPasswordAsync(Ct))
                    .Should().Be("hunter2");
            }

            EmailTestResult sent = await TestEmailAsync(admin.Client);
            sent.Should().Be(new EmailTestResult(true, admin.Email));
            sender.Sent.Should().ContainSingle().Which.To.Should().Be(admin.Email);
        }
    }

    [Fact]
    public async Task AdminSettings_RejectEnabledSmtpWithoutHostOrSender()
    {
        (WebApplicationFactory<Program> host, _) = CreateHost();
        using (host)
        {
            Account admin = await RegisterAsync(host, "admin");
            await host.Services.PromoteUserToAdminAsync(admin.Email, Ct);
            SystemSettings settings = (await admin.Client.GetFromJsonAsync<SystemSettings>("api/admin/settings", TestJson.Options, Ct))!;
            settings.Email.Enabled = true;

            HttpResponseMessage put = await admin.Client.PutAsJsonAsync("api/admin/settings", settings, TestJson.Options, Ct);

            put.IsSuccessStatusCode.Should().BeFalse();
            (await put.Content.ReadAsStringAsync(Ct)).Should().Contain("Email.Host");
        }
    }

    [Fact]
    public async Task PasswordReset_WithSmtp_EmailsAWorkingLink_AndUnknownAddressesGetNothing()
    {
        (WebApplicationFactory<Program> host, RecordingEmailSender sender) = CreateHost();
        using (host)
        {
            await ConfigureSmtpAsync(host);
            Account user = await RegisterAsync(host, "forgetful");
            sender.Clear(); // the sign-up's verification email
            HttpClient anonymous = host.CreateClient();

            (await anonymous.PostAsJsonAsync("api/auth/forgot-password", new { email = NewEmail("nobody") }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await anonymous.PostAsJsonAsync("api/auth/forgot-password", new { email = user.Email, language = "es" }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            OutboundEmail email = (await sender.WaitForAsync(user.Email))!;
            email.Should().NotBeNull("the reset email is sent in the background");
            email.Subject.Should().Be("Restablecé tu contraseña de Email Tests");
            string token = Uri.UnescapeDataString(ResetTokenInLink().Match(email.TextBody).Groups[1].Value);
            (await anonymous.PostAsJsonAsync("api/auth/reset-password", new { token, newPassword = "N3w-Passw0rd!x" }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            sender.Sent.Should().ContainSingle("no email goes to an address without an account");
        }
    }

    [Fact]
    public async Task Registration_WithSmtp_EmailsAVerificationLink_ThatVerifiesTheAccountOnce()
    {
        (WebApplicationFactory<Program> host, RecordingEmailSender sender) = CreateHost();
        using (host)
        {
            await ConfigureSmtpAsync(host, publicBaseUrl: "https://boards.example.test/");
            Account user = await RegisterAsync(host, "newcomer");
            HttpClient anonymous = host.CreateClient();

            OutboundEmail email = (await sender.WaitForAsync(user.Email))!;
            email.Should().NotBeNull();
            string token = Uri.UnescapeDataString(VerifyTokenInLink().Match(email.TextBody).Groups[1].Value);
            token.Should().NotBeEmpty();
            (await VerifiedAsync(user.Client)).Should().BeFalse();

            (await anonymous.PostAsJsonAsync("api/auth/verify-email", new { token }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await VerifiedAsync(user.Client)).Should().BeTrue();

            (await anonymous.PostAsJsonAsync("api/auth/verify-email", new { token = "not-a-token" }, Ct))
                .IsSuccessStatusCode.Should().BeFalse();
            (await user.Client.PostAsJsonAsync("api/auth/verification/resend", new { }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "the address is already verified");
        }
    }

    private static async Task<bool> VerifiedAsync(HttpClient client)
    {
        using JsonDocument doc = JsonDocument.Parse(await client.GetStringAsync("api/auth/verification", Ct));
        return doc.RootElement.GetProperty("isVerified").GetBoolean();
    }

    [GeneratedRegex(@"verify-email\?token=([^\s""]+)")]
    private static partial Regex VerifyTokenInLink();

    private (WebApplicationFactory<Program> Host, RecordingEmailSender Sender) CreateHost()
    {
        RecordingEmailSender sender = new();
        WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            }));
        return (host, sender);
    }

    private static async Task ConfigureSmtpAsync(WebApplicationFactory<Program> host, string? publicBaseUrl = null)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        ISystemSettingsService settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        SystemSettings current = await settings.GetAsync(Ct);
        current.General.InstanceTitle = "Email Tests";
        current.Email = TestEmailSettings.Configured();
        current.Email.PublicBaseUrl = publicBaseUrl;
        (await settings.UpdateAsync(current, "tests", Ct)).IsSuccess.Should().BeTrue();
    }

    private static async Task<Issued> IssueAsync(HttpClient manager, Guid workspaceId, string email, string? language = null)
    {
        HttpResponseMessage response = await manager.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/invitations/", new { email, role = "member", language }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Issued>(Ct))!;
    }

    private static async Task<EmailTestResult> TestEmailAsync(HttpClient admin)
    {
        HttpResponseMessage response = await admin.PostAsync("api/admin/settings/test-email?language=en", null, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EmailTestResult>(TestJson.Options, Ct))!;
    }

    /// <summary>The issuance payload as raw JSON values (the enum is a camelCase string on the wire).</summary>
    private sealed record Issued(Guid Id, string CleartextToken, string? AcceptUrl, string EmailStatus);

    [GeneratedRegex(@"invitations/accept\?token=(\S+)")]
    private static partial Regex TokenInLink();

    [GeneratedRegex(@"reset-password\?token=(\S+)")]
    private static partial Regex ResetTokenInLink();
}
