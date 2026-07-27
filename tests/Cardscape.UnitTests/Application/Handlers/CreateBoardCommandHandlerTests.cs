using Cardscape.Application.Boards.Commands;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class CreateBoardCommandHandlerTests
{
    [Fact]
    public async Task Handle_AsWorkspaceMember_CreatesAndPersistsBoard()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateBoardCommandHandler.Handle(
            new CreateBoardCommand(workspace.Id.Value, "Sprint", "desc",
                                  BoardVisibility.Private),
            ctx.Boards, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sprint");
        result.Value.MemberCount.Should().Be(1);
        ctx.Boards.All.Should().HaveCount(1);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AsAnonymous_ReturnsUnauthenticated()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.Anonymous();

        var result = await CreateBoardCommandHandler.Handle(
            new CreateBoardCommand(workspace.Id.Value, "Sprint", "desc",
                                  BoardVisibility.Private),
            ctx.Boards, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthenticated);
        ctx.Boards.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithMissingWorkspace_ReturnsNotFound()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateBoardCommandHandler.Handle(
            new CreateBoardCommand(Guid.NewGuid(), "Sprint", "desc",
                                  BoardVisibility.Private),
            ctx.Boards, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AsNonMember_ReturnsNotMember()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var intruder = await ctx.SeedUserAsync("intruder@example.com");
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(intruder);

        var result = await CreateBoardCommandHandler.Handle(
            new CreateBoardCommand(workspace.Id.Value, "Sprint", "desc",
                                  BoardVisibility.Private),
            ctx.Boards, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.not_member");
    }

    [Fact]
    public async Task Handle_WithBlankName_ReturnsValidationFailure()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateBoardCommandHandler.Handle(
            new CreateBoardCommand(workspace.Id.Value, "", "desc",
                                  BoardVisibility.Private),
            ctx.Boards, ctx.Workspaces, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.name.required");
    }
}
