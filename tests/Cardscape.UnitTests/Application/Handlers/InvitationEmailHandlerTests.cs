using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Email;
using Cardscape.Application.Workspaces.Commands;
using Cardscape.Contracts.Email;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// Issuing an invitation emails the invitee when SMTP is configured, and
/// always returns the link so the inviter can share it by hand when the
/// email is off or fails.
/// </summary>
public sealed class InvitationEmailHandlerTests
{
    private readonly HandlersTestContext _ctx = new();
    private readonly FakeInvitationService _invitations = new();
    private readonly RecordingEmailSender _email = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutSmtp_ReturnsTheLink_AndSendsNothing()
    {
        Workspace workspace = await SeedOwnerAsync();

        Result<WorkspaceInvitationIssuanceDto> result = await IssueAsync(workspace, "guest@example.test");

        result.IsSuccess.Should().BeTrue();
        result.Value.EmailStatus.Should().Be(EmailDeliveryStatus.NotConfigured);
        result.Value.AcceptUrl.Should().Be(
            $"https://boards.example.test/invitations/accept?token={Uri.EscapeDataString(result.Value.CleartextToken)}");
        _invitations.Issued.Should().ContainSingle();
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WithSmtp_EmailsTheInvitee_InTheInvitersLanguage()
    {
        await ConfigureSmtpAsync(instanceTitle: "Nexora Boards");
        Workspace workspace = await SeedOwnerAsync(ownerName: "Ada Lovelace", workspaceName: "Marketing");

        Result<WorkspaceInvitationIssuanceDto> result = await IssueAsync(
            workspace, "guest@example.test", WorkspaceRole.Observer, language: "es-AR");

        result.Value.EmailStatus.Should().Be(EmailDeliveryStatus.Sent);
        OutboundEmail email = _email.Sent.Should().ContainSingle().Subject;
        email.To.Should().Be("guest@example.test");
        email.Subject.Should().Be("Ada Lovelace te invitó a «Marketing» en Nexora Boards");
        email.TextBody.Should().Contain(result.Value.AcceptUrl!).And.Contain("observador");
        email.HtmlBody.Should().Contain("Aceptar la invitación");
    }

    [Fact]
    public async Task WithSmtp_AndNoLanguage_UsesTheInstanceDefault()
    {
        await ConfigureSmtpAsync(defaultLanguage: "en");
        Workspace workspace = await SeedOwnerAsync(workspaceName: "Design");

        await IssueAsync(workspace, "guest@example.test");

        OutboundEmail email = _email.Sent.Should().ContainSingle().Subject;
        email.Subject.Should().Contain("invited you to \"Design\"");
        email.TextBody.Should().Contain("as a member").And.Contain("Accept the invitation: https://boards.example.test/invitations/accept?token=");
    }

    [Fact]
    public async Task WhenTheServerRejectsTheEmail_TheInvitationStillExists_AndTheFailureIsReported()
    {
        await ConfigureSmtpAsync();
        _email.Fail = true;
        Workspace workspace = await SeedOwnerAsync();

        Result<WorkspaceInvitationIssuanceDto> result = await IssueAsync(workspace, "guest@example.test");

        result.IsSuccess.Should().BeTrue();
        result.Value.EmailStatus.Should().Be(EmailDeliveryStatus.Failed);
        result.Value.CleartextToken.Should().NotBeNullOrEmpty();
        _invitations.Issued.Should().ContainSingle();
    }

    [Fact]
    public async Task WhenThePublicAddressIsUnknown_NoEmailIsSent_AndTheFailureIsReported()
    {
        await ConfigureSmtpAsync();
        Workspace workspace = await SeedOwnerAsync();

        Result<WorkspaceInvitationIssuanceDto> result = await IssueAsync(
            workspace, "guest@example.test", links: new FakePublicLinkBuilder(baseUrl: null));

        result.Value.EmailStatus.Should().Be(EmailDeliveryStatus.Failed);
        result.Value.AcceptUrl.Should().BeNull();
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheCallerCannotManageMembers_NothingIsIssuedOrSent()
    {
        await ConfigureSmtpAsync();
        Workspace workspace = await SeedOwnerAsync();
        User stranger = await _ctx.SeedUserAsync("stranger@example.test", "Stranger");
        _ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(stranger);

        Result<WorkspaceInvitationIssuanceDto> result = await IssueAsync(workspace, "guest@example.test");

        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _invitations.Issued.Should().BeEmpty();
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public void Templates_EncodeUserSuppliedText_InTheHtmlBody()
    {
        OutboundEmail email = EmailTemplates.WorkspaceInvitation(
            "guest@example.test", "en", "Boards & <Co>", "<script>alert(1)</script>", "Q&A",
            WorkspaceRole.Member, "https://boards.example.test/invitations/accept?token=a&b", DateTimeOffset.UnixEpoch);

        email.HtmlBody.Should().NotContain("<script>")
            .And.Contain("&lt;script&gt;")
            .And.Contain("Boards &amp; &lt;Co&gt;")
            .And.Contain("href=\"https://boards.example.test/invitations/accept?token=a&amp;b\"");
        email.TextBody.Should().Contain("<script>alert(1)</script>", "the plain-text body is not HTML");
    }

    [Theory]
    [InlineData("es-AR", "en", "es")]
    [InlineData("EN", "es", "en")]
    [InlineData("fr", "es", "es")]
    [InlineData(null, "en", "en")]
    [InlineData("", "xx", "en")]
    public void ResolveLanguage_FallsBackToTheInstanceDefault(string? requested, string fallback, string expected) =>
        EmailTemplates.ResolveLanguage(requested, fallback).Should().Be(expected);

    [Fact]
    public void EmailSettings_RequireHostAndSender_OnlyWhileEnabled()
    {
        SystemSettings settings = new() { Email = { Enabled = true } };
        settings.Validate().SelectMany(error => error.MemberNames)
            .Should().BeEquivalentTo("Email.Host", "Email.FromAddress");
        settings.Email.CanSend().Should().BeFalse();

        settings.Email.Enabled = false;
        settings.Validate().Should().BeEmpty();

        settings.Email = TestEmailSettings.Configured();
        settings.Validate().Should().BeEmpty();
        settings.Email.CanSend().Should().BeTrue();
    }

    [Fact]
    public async Task SettingsService_KeepsTheSmtpPasswordWriteOnly()
    {
        InMemorySystemSettingsService service = new();
        SystemSettings settings = new() { Email = TestEmailSettings.Configured() };
        settings.Email.Password = "s3cret ";

        SystemSettings saved = (await service.UpdateAsync(settings, "admin", Ct)).Value;
        saved.Email.Password.Should().BeNull();
        saved.Email.HasPassword.Should().BeTrue();
        (await service.GetSmtpPasswordAsync(Ct)).Should().Be("s3cret ");

        saved.Email.Password = null;
        await service.UpdateAsync(saved, "admin", Ct);
        (await service.GetSmtpPasswordAsync(Ct)).Should().Be("s3cret ", "null keeps the stored password");

        saved.Email.Password = string.Empty;
        (await service.UpdateAsync(saved, "admin", Ct)).Value.Email.HasPassword.Should().BeFalse();
        (await service.GetSmtpPasswordAsync(Ct)).Should().BeNull();
    }

    private async Task ConfigureSmtpAsync(string instanceTitle = "Cardscape", string defaultLanguage = "es")
    {
        SystemSettings settings = await _ctx.Settings.GetAsync(Ct);
        settings.General.InstanceTitle = instanceTitle;
        settings.General.DefaultLanguage = defaultLanguage;
        settings.Email = TestEmailSettings.Configured();
        (await _ctx.Settings.UpdateAsync(settings, "test", Ct)).IsSuccess.Should().BeTrue();
    }

    private async Task<Workspace> SeedOwnerAsync(string ownerName = "Owner", string workspaceName = "Acme")
    {
        User owner = await _ctx.SeedUserAsync("owner@example.test", ownerName);
        _ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);
        return await _ctx.SeedWorkspaceAsync(owner.Id.Value, workspaceName);
    }

    private Task<Result<WorkspaceInvitationIssuanceDto>> IssueAsync(
        Workspace workspace,
        string email,
        WorkspaceRole role = WorkspaceRole.Member,
        string? language = null,
        FakePublicLinkBuilder? links = null) =>
        IssueWorkspaceInvitationCommandHandler.HandleAsync(
            new IssueWorkspaceInvitationCommand(workspace.Id.Value, email, role, Language: language),
            _invitations,
            _ctx.Workspaces,
            _ctx.Boards,
            _ctx.Users,
            _ctx.Settings,
            _ctx.CurrentUser,
            _email,
            links ?? new FakePublicLinkBuilder(),
            _ctx.Clock,
            Ct);
}
