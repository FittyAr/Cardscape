using Cardscape.Application.Boards.Commands;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class StarBoardCommandHandlerTests
{
    [Fact]
    public async Task Handle_OnExistingBoard_StarsAndPersists()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await StarBoardCommandHandler.Handle(
            new StarBoardCommand(board.Id.Value),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsStarred.Should().BeTrue();
        board.IsStarredBy(user.Id.Value).Should().BeTrue();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnMissingBoard_ReturnsNotFound()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await StarBoardCommandHandler.Handle(
            new StarBoardCommand(Guid.NewGuid()),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AsAnonymous_ReturnsUnauthenticated()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.Anonymous();

        var result = await StarBoardCommandHandler.Handle(
            new StarBoardCommand(board.Id.Value),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthenticated);
    }
}
