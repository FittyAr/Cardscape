using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Tests.Common.Fakes;

/// <summary>
/// Aggregates the fakes needed to drive a Wolverine handler
/// invocation from a unit test. Build one per test, mutate as
/// needed, then call the static <c>Handle</c> method on the
/// handler under test.
/// </summary>
public sealed class HandlersTestContext
{
    public FakeClock Clock { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public FakePasswordHasher PasswordHasher { get; } = new();
    public FakeTokenService Tokens { get; } = new();
    public FakeCurrentUser CurrentUser { get; set; } = FakeCurrentUser.Anonymous();
    public FakeDomainEventDispatcher EventDispatcher { get; } = new();

    public InMemoryUserRepository Users { get; } = new();
    public InMemoryWorkspaceRepository Workspaces { get; } = new();
    public InMemoryBoardRepository Boards { get; } = new();
    public InMemoryCardRepository Cards { get; } = new();
    public InMemoryBoardListRepository Lists { get; } = new();
    public InMemoryLabelRepository Labels { get; } = new();

    /// <summary>Builds a persisted <see cref="User"/> in the fake
    /// repository and returns it. Useful for tests that need a
    /// known user to log in or assign to a card.</summary>
    public async Task<User> SeedUserAsync(string email = "alice@example.com",
                                           string displayName = "Alice",
                                           string password = "Passw0rd!",
                                           bool active = true)
    {
        var hash = PasswordHasher.Hash(password);
        var user = User.Register(UserId.New(), EmailAddress.Create(email).Value,
                                 DisplayName.Create(displayName).Value, hash, Clock.UtcNow).Value;
        if (!active)
        {
            user.Deactivate(Clock.UtcNow);
        }
        await Users.AddAsync(user);
        return user;
    }

    /// <summary>Builds a workspace owned by <paramref name="ownerId"/>
    /// in the fake repository and returns it.</summary>
    public async Task<Workspace> SeedWorkspaceAsync(Guid ownerId, string name = "Acme")
    {
        var ws = Workspace.Create(WorkspaceId.New(),
                                  WorkspaceName.Create(name).Value,
                                  ownerId, Clock.UtcNow).Value;
        await Workspaces.AddAsync(ws);
        return ws;
    }

    /// <summary>Builds a board in a workspace, owned by the given user.</summary>
    public async Task<Board> SeedBoardAsync(WorkspaceId workspaceId, Guid ownerId, string name = "My Board")
    {
        var board = Board.Create(BoardId.New(), workspaceId,
                                 BoardName.Create(name).Value,
                                 BoardDescription.Create("d").Value,
                                 BoardVisibility.Private, ownerId, Clock.UtcNow).Value;
        await Boards.AddAsync(board);
        return board;
    }

    /// <summary>Builds a list inside a board.</summary>
    public async Task<BoardList> SeedListAsync(BoardId boardId, string name = "To Do")
    {
        var list = BoardList.Create(
            id: BoardListId.New(),
            boardId: boardId,
            name: ListName.Create(name).Value,
            position: Position.Start(),
            createdBy: Guid.NewGuid(),
            at: Clock.UtcNow).Value;
        await Lists.AddAsync(list);
        return list;
    }

    /// <summary>Builds a card inside a list.</summary>
    public async Task<Card> SeedCardAsync(BoardListId listId, Guid creatorId, string title = "My Card")
    {
        var card = Card.Create(CardId.New(), listId,
                               CardTitle.Create(title).Value,
                               CardDescription.Create("d").Value,
                               Position.Start(), creatorId, Clock.UtcNow).Value;
        await Cards.AddAsync(card);
        return card;
    }
}
