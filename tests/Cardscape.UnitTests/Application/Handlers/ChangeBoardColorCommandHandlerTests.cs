using Cardscape.Application.Boards.Commands;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class ChangeBoardColorCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithPaletteName_SetsHexColorOnBoardAndDto()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await ChangeBoardColorCommandHandler.HandleAsync(
            new ChangeBoardColorCommand(board.Id.Value, "Blue"),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Color.Should().Be(Color.Palette.Blue.Value);
        board.Color.Should().Be(Color.Palette.Blue);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    public async Task Handle_WithNoneOrNull_ClearsColor(string? colorName)
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        board.ChangeColor(Color.Palette.Green, ctx.Clock.UtcNow);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await ChangeBoardColorCommandHandler.HandleAsync(
            new ChangeBoardColorCommand(board.Id.Value, colorName),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Color.Should().BeNull();
        board.Color.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithUnknownColorName_ReturnsValidationFailure()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync();
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, user.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

        var result = await ChangeBoardColorCommandHandler.HandleAsync(
            new ChangeBoardColorCommand(board.Id.Value, "#123456"),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.color_invalid");
        board.Color.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AsNonMember_IsRejected()
    {
        var ctx = new HandlersTestContext();
        var owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        var intruder = await ctx.SeedUserAsync("intruder@example.com", "Intruder");
        var workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        var board = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value);
        ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(intruder);

        var result = await ChangeBoardColorCommandHandler.HandleAsync(
            new ChangeBoardColorCommand(board.Id.Value, "blue"),
            ctx.Boards, ctx.UnitOfWork, ctx.CurrentUser, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        board.Color.Should().BeNull();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }
}
