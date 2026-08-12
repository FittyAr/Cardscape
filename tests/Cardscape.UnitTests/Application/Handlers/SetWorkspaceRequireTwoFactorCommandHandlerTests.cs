using Cardscape.Application.Workspaces.Commands;
using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public sealed class SetWorkspaceRequireTwoFactorCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOwnerEnrollmentIsPending_LeavesPolicyDisabled()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("pending-owner@example.com", "Owner", "Passw0rd!");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        await ctx.SeedTotpCredentialAsync(owner, confirmed: false);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await SetWorkspaceRequireTwoFactorCommandHandler.Handle(
            new SetWorkspaceRequireTwoFactorCommand(workspace.Id.Value, Require: true),
            ctx.Workspaces,
            ctx.TotpCredentials,
            ctx.UnitOfWork,
            ctx.CurrentUser,
            ctx.Clock,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.totp.workspace_enrollment_incomplete");
        workspace.RequireTwoFactor.Should().BeFalse();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAnotherMemberIsNotEnrolled_LeavesPolicyDisabled()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner", "Passw0rd!");
        var member = await ctx.SeedUserAsync("member@example.com", "Member", "Passw0rd!");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        workspace.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        await ctx.SeedTotpCredentialAsync(owner);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await SetWorkspaceRequireTwoFactorCommandHandler.Handle(
            new SetWorkspaceRequireTwoFactorCommand(workspace.Id.Value, Require: true),
            ctx.Workspaces,
            ctx.TotpCredentials,
            ctx.UnitOfWork,
            ctx.CurrentUser,
            ctx.Clock,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.totp.workspace_enrollment_incomplete");
        workspace.RequireTwoFactor.Should().BeFalse();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }
}
