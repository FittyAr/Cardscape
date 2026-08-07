using Cardscape.Application.Cards.Commands;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class CreateCardCommandHandlerTests
{
    [Fact]
    public async Task Handle_AsAuthenticatedUser_CreatesAndPersistsCard()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateCardCommandHandler.Handle(
            new CreateCardCommand(list.Id.Value, "Buy milk", "details"),
            ctx.Lists, ctx.Boards, ctx.Cards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Buy milk");
        result.Value.ListId.Should().Be(list.Id.Value);
        ctx.Cards.All.Should().HaveCount(1);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AsAnonymous_ReturnsUnauthenticated()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        ctx.CurrentUser = FakeCurrentUser.Anonymous();

        var result = await CreateCardCommandHandler.Handle(
            new CreateCardCommand(list.Id.Value, "Buy milk", "details"),
            ctx.Lists, ctx.Boards, ctx.Cards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthenticated);
    }

    [Fact]
    public async Task Handle_OnMissingList_ReturnsNotFound()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateCardCommandHandler.Handle(
            new CreateCardCommand(Guid.NewGuid(), "Buy milk", "details"),
            ctx.Lists, ctx.Boards, ctx.Cards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WithBlankTitle_ReturnsValidationFailure()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await CreateCardCommandHandler.Handle(
            new CreateCardCommand(list.Id.Value, "", "details"),
            ctx.Lists, ctx.Boards, ctx.Cards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cards.title.required");
    }

    [Fact]
    public async Task Handle_AsNonMember_ReturnsForbidden()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var intruder = await ctx.SeedUserAsync("intruder@example.com", "Intruder");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        var list = await ctx.SeedListAsync(board.Id);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(intruder);

        var result = await CreateCardCommandHandler.Handle(
            new CreateCardCommand(list.Id.Value, "Sneaky", "details"),
            ctx.Lists, ctx.Boards, ctx.Cards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, ctx.SearchIndex, ctx.Activities, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}
