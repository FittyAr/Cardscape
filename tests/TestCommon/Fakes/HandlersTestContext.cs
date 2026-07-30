using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.Abstractions;
using Cardscape.Domain.Authentication.Totp;
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
    public FakeDeploymentRegion DeploymentRegion { get; } = new();
    public IdentitySecretProtector SecretProtector { get; } = new();
    public InMemoryPendingTotpLoginStore PendingTotpLogins { get; } = new();

    public InMemoryUserRepository Users { get; } = new();
    public InMemoryWorkspaceRepository Workspaces { get; } = new();
    public InMemoryBoardRepository Boards { get; } = new();
    public InMemoryCardRepository Cards { get; } = new();
    public InMemoryBoardListRepository Lists { get; } = new();
    public InMemoryLabelRepository Labels { get; } = new();
    public InMemoryTotpCredentialRepository TotpCredentials { get; } = new();

    /// <summary>
    /// Lazily-built <see cref="FakeTotpService"/>. The first
    /// access wires the dependencies (credentials, secret
    /// protector, clock, unit of work) so tests that do not use
    /// 2FA never pay the cost of constructing it.
    /// </summary>
    private FakeTotpService? _totpService;
    public ITotpService TotpService =>
        _totpService ??= new FakeTotpService(
            TotpCredentials, SecretProtector, Clock, UnitOfWork);

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

    /// <summary>Enrols a 2FA credential for <paramref name="user"/>
    /// using a deterministic base32 secret. The returned
    /// <see cref="TotpCredential"/> is the one stored in the
    /// in-memory repository; <paramref name="secret"/> is the
    /// cleartext that <see cref="IdentitySecretProtector"/>
    /// "stores" verbatim.</summary>
    public async Task<(TotpCredential Credential, string Secret)> SeedTotpCredentialAsync(
        User user, string secret = "JBSWY3DPEHPK3PXP")
    {
        var enrolled = TotpCredential.Enroll(
            userId: user.Id,
            encryptedSecret: SecretProtector.Protect(secret),
            recoveryCodesHash: string.Join('\n',
                Enumerable.Range(0, TotpCredential.RecoveryCodeCount)
                    .Select(i => Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes($"recovery-{i}")))
                        .ToLowerInvariant())),
            at: Clock.UtcNow).Value;
        await TotpCredentials.AddAsync(enrolled);
        return (enrolled, secret);
    }

    /// <summary>Builds a workspace owned by <paramref name="ownerId"/>
    /// in the fake repository and returns it.</summary>
    public async Task<Workspace> SeedWorkspaceAsync(Guid ownerId, string name = "Acme")
    {
        var ws = Workspace.Create(WorkspaceId.New(),
                                  WorkspaceName.Create(name).Value,
                                  ownerId,
                                  Region.Unspecified,
                                  Clock.UtcNow).Value;
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
