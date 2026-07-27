using Cardscape.Application.Workspaces.Commands;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class CreateWorkspaceCommandHandlerTests
{
    [Fact]
    public async Task Handle_AsAuthenticatedUser_CreatesAndPersistsWorkspace()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateWorkspaceCommandHandler.Handle(
            new CreateWorkspaceCommand("Acme"),
            ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerId.Should().Be(user.Id.Value);
        result.Value.MemberCount.Should().Be(1);
        ctx.Workspaces.All.Should().HaveCount(1);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AsAnonymous_ReturnsUnauthenticated()
    {
        var ctx = new HandlersTestContext { CurrentUser = FakeCurrentUser.Anonymous() };

        var result = await CreateWorkspaceCommandHandler.Handle(
            new CreateWorkspaceCommand("Acme"),
            ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthenticated);
    }

    [Fact]
    public async Task Handle_WithBlankName_ReturnsValidationFailure()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateWorkspaceCommandHandler.Handle(
            new CreateWorkspaceCommand(string.Empty),
            ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.name.required");
    }
}
