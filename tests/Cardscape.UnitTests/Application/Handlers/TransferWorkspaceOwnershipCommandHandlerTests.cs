using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Users.Commands;
using Cardscape.Application.Workspaces.Commands;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using Moq;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// Workspace ownership transfer (owner or instance admin only) and
/// the orphan protection on deactivate / soft-delete / anonymise.
/// </summary>
public sealed class TransferWorkspaceOwnershipCommandHandlerTests
{
    [Fact]
    public async Task Owner_TransfersToAMember_AndStaysAsAdmin()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        ws.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await TransferAsync(ctx, ws, member.Id.Value);

        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerId.Should().Be(member.Id.Value);
        ws.Members.Single(m => m.UserId == owner.Id.Value).Role.Should().Be(WorkspaceRole.Admin);
        ws.Members.Single(m => m.UserId == member.Id.Value).Role.Should().Be(WorkspaceRole.Admin);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task WorkspaceAdminWhoIsNotTheOwner_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User admin = await ctx.SeedUserAsync("admin@example.com", "Admin");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        ws.AddMember(admin.Id.Value, WorkspaceRole.Admin, ctx.Clock.UtcNow);
        ws.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var toSelf = await TransferAsync(ctx, ws, admin.Id.Value);
        var toOther = await TransferAsync(ctx, ws, member.Id.Value);

        toSelf.Error.Code.Should().Be("workspaces.forbidden");
        toOther.Error.Code.Should().Be("workspaces.forbidden");
        ws.OwnerId.Should().Be(owner.Id.Value);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ActiveInstanceAdmin_CanTransferAWorkspaceTheyDoNotBelongTo()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        User root = await ctx.SeedUserAsync("root@example.com", "Root");
        root.SetAdmin(true, ctx.Clock.UtcNow);
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        ws.AddMember(member.Id.Value, WorkspaceRole.Observer, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(root);

        var result = await TransferAsync(ctx, ws, member.Id.Value);

        result.IsSuccess.Should().BeTrue();
        ws.OwnerId.Should().Be(member.Id.Value);
        ws.HasMember(root.Id.Value).Should().BeFalse("the admin override does not add the admin as a member");
    }

    [Fact]
    public async Task DeactivatedInstanceAdmin_GetsNoOverride()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        User root = await ctx.SeedUserAsync("root@example.com", "Root");
        root.SetAdmin(true, ctx.Clock.UtcNow);
        root.Deactivate(ctx.Clock.UtcNow);
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        ws.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(root);

        var result = await TransferAsync(ctx, ws, member.Id.Value);

        result.Error.Code.Should().Be("workspaces.forbidden");
    }

    [Fact]
    public async Task TransferToANonMember_IsRefusedByTheAggregate()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User stranger = await ctx.SeedUserAsync("stranger@example.com", "Stranger");
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await TransferAsync(ctx, ws, stranger.Id.Value);

        result.Error.Code.Should().Be("workspaces.ownership.not_member");
    }

    [Fact]
    public async Task TransferToADeactivatedMember_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User dormant = await ctx.SeedUserAsync("dormant@example.com", "Dormant", active: false);
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        ws.AddMember(dormant.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(owner);

        var result = await TransferAsync(ctx, ws, dormant.Id.Value);

        result.Error.Code.Should().Be("workspaces.ownership.inactive_user");
        ws.OwnerId.Should().Be(owner.Id.Value);
    }

    [Fact]
    public async Task Anonymous_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        Workspace ws = await ctx.SeedWorkspaceAsync(owner.Id.Value);

        var result = await TransferAsync(ctx, ws, owner.Id.Value);

        result.Error.Type.Should().Be(ErrorType.Unauthenticated);
    }

    // ── orphan protection ─────────────────────────────────────

    [Fact]
    public async Task SoftDelete_OwnerOfASharedWorkspace_IsRefusedWithTheWorkspaceNames()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace shared = await ctx.SeedWorkspaceAsync(owner.Id.Value, "Shared team");
        shared.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        await ctx.SeedWorkspaceAsync(owner.Id.Value, "Solo notes");

        Result result = await SoftDeleteAsync(ctx, owner);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.owns_workspaces");
        result.Error.Message.Should().Contain("Shared team").And.NotContain("Solo notes");
        result.Error.Details!["workspaces"].Should().BeEquivalentTo(new[] { "Shared team" });
        owner.IsDeleted.Should().BeFalse();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_SoleMemberWorkspaces_KeepTheExistingBehaviour()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User formerMember = await ctx.SeedUserAsync("gone@example.com", "Gone", active: false);
        Workspace solo = await ctx.SeedWorkspaceAsync(owner.Id.Value, "Solo notes");
        Workspace dormant = await ctx.SeedWorkspaceAsync(owner.Id.Value, "Only inactive people");
        dormant.AddMember(formerMember.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);

        Result result = await SoftDeleteAsync(ctx, owner);

        result.IsSuccess.Should().BeTrue();
        owner.IsDeleted.Should().BeTrue();
        solo.OwnerId.Should().Be(owner.Id.Value);
        solo.IsDeleted.Should().BeFalse("the workspace is left in place, still owned by the deleted account");
        solo.HasMember(owner.Id.Value).Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_IsAllowedOnceOwnershipHasBeenTransferred()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace shared = await ctx.SeedWorkspaceAsync(owner.Id.Value, "Shared team");
        shared.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        shared.TransferOwnership(member.Id.Value, owner.Id.Value, ctx.Clock.UtcNow).IsSuccess.Should().BeTrue();

        Result result = await SoftDeleteAsync(ctx, owner);

        result.IsSuccess.Should().BeTrue();
        owner.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_OwnerOfASharedWorkspace_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User root = await ctx.SeedUserAsync("root@example.com", "Root");
        root.SetAdmin(true, ctx.Clock.UtcNow);
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace shared = await ctx.SeedWorkspaceAsync(owner.Id.Value, "Shared team");
        shared.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(root);

        Result result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(owner.Id.Value, false),
            ctx.Users, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.Error.Code.Should().Be("users.owns_workspaces");
        owner.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Anonymise_OwnerOfASharedWorkspace_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace shared = await ctx.SeedWorkspaceAsync(owner.Id.Value, "Shared team");
        shared.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);

        Result result = await AnonymiseUserCommandHandler.HandleAsync(
            new AnonymiseUserCommand(owner.Id.Value),
            ctx.Users, ctx.Workspaces, Mock.Of<IUserPreferencesRepository>(),
            ctx.UnitOfWork, ctx.Clock, CancellationToken.None);

        result.Error.Code.Should().Be("users.owns_workspaces");
        owner.IsAnonymised.Should().BeFalse();
    }

    [Fact]
    public async Task LastAdminGuard_StillTakesPrecedence()
    {
        var ctx = new HandlersTestContext();
        User root = await ctx.SeedUserAsync("root@example.com", "Root");
        root.SetAdmin(true, ctx.Clock.UtcNow);
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        Workspace shared = await ctx.SeedWorkspaceAsync(root.Id.Value, "Shared team");
        shared.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);

        Result result = await SoftDeleteAsync(ctx, root);

        result.Error.Code.Should().Be("users.last_admin");
    }

    private static Task<Result<Cardscape.Application.Workspaces.DTOs.WorkspaceDto>> TransferAsync(
        HandlersTestContext ctx, Workspace ws, Guid newOwnerId) =>
        TransferWorkspaceOwnershipCommandHandler.HandleAsync(
            new TransferWorkspaceOwnershipCommand(ws.Id.Value, newOwnerId),
            ctx.Workspaces, ctx.Users, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

    private static Task<Result> SoftDeleteAsync(HandlersTestContext ctx, User user) =>
        SoftDeleteUserCommandHandler.HandleAsync(
            new SoftDeleteUserCommand(user.Id.Value),
            ctx.Users, ctx.Workspaces, Mock.Of<IUserPreferencesRepository>(),
            ctx.UnitOfWork, ctx.Clock, CancellationToken.None);
}
