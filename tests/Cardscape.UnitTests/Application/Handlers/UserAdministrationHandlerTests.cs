using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Users.Commands;
using Cardscape.Application.Users.Queries;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// Instance user administration: the last-admin guard on every
/// command that can take an administrator out of play, the
/// deactivate / reactivate command, and the admin directory query.
/// </summary>
public sealed class UserAdministrationHandlerTests
{
    [Fact]
    public async Task SetUserAdmin_RevokingTheLastActiveAdmin_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        User other = await ctx.SeedUserAsync("bob@example.com", "Bob");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var result = await SetUserAdminCommandHandler.HandleAsync(
            new SetUserAdminCommand(admin.Id.Value, false),
            ctx.Users, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.last_admin");
        admin.IsAdmin.Should().BeTrue();
        other.IsAdmin.Should().BeFalse();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SetUserAdmin_SelfDemotion_IsAllowedWhenAnotherAdminRemains()
    {
        var ctx = new HandlersTestContext();
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        await SeedAdminAsync(ctx, "second@example.com");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var result = await SetUserAdminCommandHandler.HandleAsync(
            new SetUserAdminCommand(admin.Id.Value, false),
            ctx.Users, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        admin.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task SetUserAdmin_DeactivatedAdminsDoNotCountTowardsTheQuorum()
    {
        var ctx = new HandlersTestContext();
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        User dormant = await SeedAdminAsync(ctx, "dormant@example.com");
        dormant.Deactivate(ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var result = await SetUserAdminCommandHandler.HandleAsync(
            new SetUserAdminCommand(admin.Id.Value, false),
            ctx.Users, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.last_admin");
    }

    [Fact]
    public async Task SetUserActive_DeactivatesAndReactivatesAnotherUser()
    {
        var ctx = new HandlersTestContext();
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        User bob = await ctx.SeedUserAsync("bob@example.com", "Bob");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var deactivate = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(bob.Id.Value, false),
            ctx.Users, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);
        deactivate.IsSuccess.Should().BeTrue();
        bob.IsActive.Should().BeFalse();

        var reactivate = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(bob.Id.Value, true),
            ctx.Users, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);
        reactivate.IsSuccess.Should().BeTrue();
        bob.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetUserActive_SelfDeactivation_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        await SeedAdminAsync(ctx, "second@example.com");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(admin.Id.Value, false),
            ctx.Users, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.self_deactivation");
        admin.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetUserActive_DeactivatingTheLastAdmin_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User caller = await ctx.SeedUserAsync("caller@example.com", "Caller");
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(caller);

        var result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(admin.Id.Value, false),
            ctx.Users, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.last_admin");
    }

    [Fact]
    public async Task SetUserActive_ReactivatingADeletedUser_IsRefused()
    {
        var ctx = new HandlersTestContext();
        User admin = await SeedAdminAsync(ctx, "root@example.com");
        User bob = await ctx.SeedUserAsync("bob@example.com", "Bob");
        bob.SoftDelete(ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(admin);

        var result = await SetUserActiveCommandHandler.HandleAsync(
            new SetUserActiveCommand(bob.Id.Value, true),
            ctx.Users, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.deleted");
    }

    [Fact]
    public async Task ListUsersForAdmin_FiltersBySearchAndStatus_AndPages()
    {
        var ctx = new HandlersTestContext();
        await SeedAdminAsync(ctx, "root@example.com");
        User ana = await ctx.SeedUserAsync("ana@acme.test", "Ana Pérez");
        User bruno = await ctx.SeedUserAsync("bruno@acme.test", "Bruno");
        await ctx.SeedUserAsync("carla@other.test", "Carla");
        bruno.Deactivate(ctx.Clock.UtcNow);

        AdminUserPageDto acme = await ListUsersForAdminQueryHandler.HandleAsync(
            new ListUsersForAdminQuery("ACME"), ctx.Users, CancellationToken.None);
        acme.Total.Should().Be(2);
        acme.Items.Select(u => u.Email).Should().Equal("ana@acme.test", "bruno@acme.test");

        AdminUserPageDto byName = await ListUsersForAdminQueryHandler.HandleAsync(
            new ListUsersForAdminQuery("pérez"), ctx.Users, CancellationToken.None);
        byName.Items.Should().ContainSingle().Which.Id.Should().Be(ana.Id.Value);

        AdminUserPageDto deactivated = await ListUsersForAdminQueryHandler.HandleAsync(
            new ListUsersForAdminQuery(null, UserStatusFilter.Deactivated), ctx.Users, CancellationToken.None);
        deactivated.Items.Should().ContainSingle().Which.Id.Should().Be(bruno.Id.Value);

        AdminUserPageDto secondPage = await ListUsersForAdminQueryHandler.HandleAsync(
            new ListUsersForAdminQuery(null, UserStatusFilter.All, Page: 1, PageSize: 3), ctx.Users, CancellationToken.None);
        secondPage.Total.Should().Be(4);
        secondPage.Items.Should().ContainSingle();
    }

    private static async Task<User> SeedAdminAsync(HandlersTestContext ctx, string email)
    {
        User user = await ctx.SeedUserAsync(email, email.Split('@')[0]);
        user.SetAdmin(true, ctx.Clock.UtcNow);
        return user;
    }
}
