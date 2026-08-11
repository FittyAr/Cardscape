using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;
using Cardscape.Mcp.Realtime;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Moq;

namespace Cardscape.UnitTests.Security;

public sealed class McpResourceBroadcasterAuthorizationTests
{
    [Fact]
    public async Task BroadcastAsync_WhenSubscriberLostPrivateBoardAccess_RemovesSubscription()
    {
        Guid boardId = Guid.NewGuid();
        Guid formerMemberId = Guid.NewGuid();
        Board privateBoard = Board.Create(
            new BoardId(boardId),
            WorkspaceId.New(),
            BoardName.Create("Private board").Value,
            BoardDescription.Create(null).Value,
            BoardVisibility.Private,
            Guid.NewGuid(),
            DateTimeOffset.Parse("2026-08-11T12:00:00Z")).Value;
        var boards = new Mock<IBoardRepository>();
        boards.Setup(repository => repository.GetByIdAsync(
                new BoardId(boardId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateBoard);

        var services = new ServiceCollection();
        services.AddScoped(_ => boards.Object);
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using var broadcaster = new McpResourceBroadcaster(
            NullLogger<McpResourceBroadcaster>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
        McpServer server = new Mock<McpServer>().Object;
        string uri = $"board://{boardId:N}";
        broadcaster.Subscribe(uri, server, formerMemberId);

        await broadcaster.BroadcastAsync(boardId, TestContext.Current.CancellationToken);

        broadcaster.GetSnapshot().Subscribers.Should().NotContainKey(uri);
        boards.Verify(repository => repository.GetByIdAsync(
            new BoardId(boardId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
