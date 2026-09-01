using System.Globalization;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Cardscape.Mcp.Authorization;
using FluentAssertions;
using Moq;

namespace Cardscape.UnitTests.Security;

public sealed class McpBoardSubscriptionAuthorizationTests
{
    [Fact]
    public async Task AuthorizeAsync_PrivateBoardMember_ReturnsCanonicalUri()
    {
        Guid userId = Guid.NewGuid();
        Board board = CreateBoard(userId, BoardVisibility.Private);
        Mock<IBoardRepository> boards = RepositoryReturning(board);

        string result = await McpBoardSubscriptionAuthorization.AuthorizeAsync(
            $"board://{board.Id.Value:D}", userId, boards.Object, TestContext.Current.CancellationToken);

        result.Should().Be($"board://{board.Id.Value:N}");
    }

    [Fact]
    public async Task AuthorizeAsync_PublicBoardNonMember_AllowsSubscription()
    {
        Board board = CreateBoard(Guid.NewGuid(), BoardVisibility.Public);
        Mock<IBoardRepository> boards = RepositoryReturning(board);

        Func<Task> act = async () => await McpBoardSubscriptionAuthorization.AuthorizeAsync(
            $"board://{board.Id.Value:N}", Guid.NewGuid(), boards.Object, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AuthorizeAsync_PrivateBoardNonMember_DeniesSubscription()
    {
        Board board = CreateBoard(Guid.NewGuid(), BoardVisibility.Private);
        Mock<IBoardRepository> boards = RepositoryReturning(board);

        Func<Task> act = async () => await McpBoardSubscriptionAuthorization.AuthorizeAsync(
            $"board://{board.Id.Value:N}", Guid.NewGuid(), boards.Object, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpBoardSubscriptionAuthorization.ForbiddenErrorCode}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("card://8de61e82-2b3c-4595-93f7-13bdad5ac98b")]
    [InlineData("board://not-a-guid")]
    [InlineData("board://00000000-0000-0000-0000-000000000000")]
    public void ParseBoardId_InvalidOrUnsupportedUri_DeniesSubscription(string? uri)
    {
        Action act = () => McpBoardSubscriptionAuthorization.ParseBoardId(uri);

        act.Should().Throw<ModelContextProtocol.McpException>()
            .WithMessage($"*{McpBoardSubscriptionAuthorization.InvalidResourceErrorCode}*");
    }

    private static Board CreateBoard(Guid creatorId, BoardVisibility visibility) =>
        Board.Create(
            BoardId.New(),
            WorkspaceId.New(),
            BoardName.Create("Subscription board").Value,
            BoardDescription.Create(null).Value,
            visibility,
            creatorId,
            DateTimeOffset.Parse("2026-08-11T12:00:00Z", CultureInfo.InvariantCulture)).Value;

    private static Mock<IBoardRepository> RepositoryReturning(Board board)
    {
        var repository = new Mock<IBoardRepository>();
        repository.Setup(item => item.GetByIdAsync(board.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        return repository;
    }
}
