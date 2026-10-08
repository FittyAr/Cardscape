using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Sign-in through an identity provider must respect
/// <c>AllowPublicRegistration</c>: a brand-new user is provisioned only
/// when the instance accepts sign-ups or the email has a pending
/// workspace invitation.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class ExternalLoginProvisioningTests(CardscapeWebApplicationFactory factory)
{
    [Fact]
    public async Task ExternalLogin_DoesNotProvisionUninvitedUsers_WhenRegistrationIsClosed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(_ => { });
        WorkspaceMemberAdministrationTests.Account owner = await WorkspaceMemberAdministrationTests.RegisterAsync(host, "owner");
        WorkspaceDto ws = await WorkspaceMemberAdministrationTests.CreateWorkspaceAsync(owner.Client, "SSO invite");
        await WorkspaceMemberAdministrationTests.SetPublicRegistrationAsync(host, allow: false);
        string invitedEmail = WorkspaceMemberAdministrationTests.NewEmail("sso-invited");
        await WorkspaceMemberAdministrationTests.InviteAsync(owner.Client, ws.Id, invitedEmail, "member");

        Result<ExternalLoginResolution> uninvited = await ResolveExternalAsync(host, WorkspaceMemberAdministrationTests.NewEmail("sso-stranger"));
        uninvited.IsFailure.Should().BeTrue();
        uninvited.Error.Code.Should().Be("Auth.RegistrationClosed");

        Result<ExternalLoginResolution> invited = await ResolveExternalAsync(host, invitedEmail);
        invited.IsSuccess.Should().BeTrue();
        invited.Value.IsNewUser.Should().BeTrue();

        // Existing accounts can still link a provider.
        Result<ExternalLoginResolution> existing = await ResolveExternalAsync(host, owner.Email);
        existing.IsSuccess.Should().BeTrue();
        existing.Value.IsNewUser.Should().BeFalse();
    }

    private static async Task<Result<ExternalLoginResolution>> ResolveExternalAsync(WebApplicationFactory<Program> host, string email)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IExternalLoginService service = scope.ServiceProvider.GetRequiredService<IExternalLoginService>();
        return await service.ResolveAsync(
            ExternalProvider.Google,
            SubjectId.Create($"sub-{Guid.NewGuid():N}").Value,
            email,
            "SSO User",
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
    }
}
