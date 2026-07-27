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

/// <summary>Deterministic clock. Pin the time in tests so the asserted
/// values don't drift with the wall clock.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset? start = null) => UtcNow = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>No-op unit of work. Records call count for assertions.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(SaveChangesCallCount);
    }
}

/// <summary>Deterministic password hasher. The hash is <c>v1.{plaintext}</c>
/// so tests can both create and verify.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public PasswordHash Hash(string plaintext) =>
        PasswordHash.FromHashed($"{PasswordHash.VersionPrefix}.{plaintext}").Value;

    public bool Verify(string plaintext, PasswordHash hash) =>
        hash.Value == $"{PasswordHash.VersionPrefix}.{plaintext}";
}

/// <summary>Predictable token service. Tokens are the user id encoded
/// as base64; refresh tokens are GUIDs. Tracks issued tokens for
/// assertions.</summary>
public sealed class FakeTokenService : ITokenService
{
    public List<(User User, IReadOnlyCollection<string> Roles)> AccessTokensIssued { get; } = [];

    public List<RefreshToken> RefreshTokensIssued { get; } = [];

    public string IssueAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        AccessTokensIssued.Add((user, roles));
        return Convert.ToBase64String(user.Id.Value.ToByteArray());
    }

    public RefreshToken IssueRefreshToken()
    {
        var token = new RefreshToken(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddDays(30));
        RefreshTokensIssued.Add(token);
        return token;
    }

    public Guid? GetUserIdFromToken(string token)
    {
        try
        {
            return new Guid(Convert.FromBase64String(token));
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Settable current user. The handler reads from this in
/// every test scenario.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; }
    public UserId? Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];

    public static FakeCurrentUser Anonymous() => new() { IsAuthenticated = false };

    public static FakeCurrentUser AuthenticatedAs(User user, params string[] roles) => new()
    {
        IsAuthenticated = true,
        Id = user.Id,
        Email = user.Email.Value,
        DisplayName = user.DisplayName.Value,
        Roles = roles
    };
}

/// <summary>Records every dispatched event. Tests assert on the
/// collected events.</summary>
public sealed class FakeDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = [];

    public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        Dispatched.AddRange(events);
        return Task.CompletedTask;
    }
}

/// <summary>Generic in-memory implementation of <see cref="IRepository{T, TId}"/>.
/// Backed by a <see cref="Dictionary{TKey, TValue}"/>; safe for
/// single-threaded tests.</summary>
public class InMemoryRepositoryBase<T, TId> : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
{
    protected readonly Dictionary<TId, T> Store = [];

    public IReadOnlyCollection<T> All => Store.Values.ToList();

    public Task<T?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        Task.FromResult(Store.GetValueOrDefault(id));

    public Task AddAsync(T aggregate, CancellationToken ct = default)
    {
        Store[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public void Remove(T aggregate) => Store.Remove(aggregate.Id);
}

/// <summary>In-memory <see cref="IUserRepository"/>. Supports
/// <see cref="FindByEmailAsync"/> with case-insensitive match.</summary>
public sealed class InMemoryUserRepository : InMemoryRepositoryBase<User, UserId>, IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        return Task.FromResult(Store.Values.FirstOrDefault(u =>
            string.Equals(u.Email.Value, normalized, StringComparison.OrdinalIgnoreCase)));
    }
}

/// <summary>In-memory <see cref="IWorkspaceRepository"/>.</summary>
public sealed class InMemoryWorkspaceRepository : InMemoryRepositoryBase<Workspace, WorkspaceId>, IWorkspaceRepository
{
    public Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<Workspace> rows = Store.Values
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<Workspace?> GetWithMembersAsync(WorkspaceId id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);
}

/// <summary>In-memory <see cref="IBoardRepository"/>.</summary>
public sealed class InMemoryBoardRepository : InMemoryRepositoryBase<Board, BoardId>, IBoardRepository
{
    public Task<IReadOnlyList<Board>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default)
    {
        IReadOnlyList<Board> rows = Store.Values
            .Where(b => b.WorkspaceId.Value == workspaceId.Value)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<Board>> ListStarredByUserAsync(Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<Board> rows = Store.Values
            .Where(b => b.Stars.Any(s => s.UserId == userId))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<Board?> GetWithMembersAsync(BoardId id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);
}

/// <summary>In-memory <see cref="ICardRepository"/>.</summary>
public sealed class InMemoryCardRepository : InMemoryRepositoryBase<Card, CardId>, ICardRepository
{
    public Task<IReadOnlyList<Card>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default)
    {
        IReadOnlyList<Card> rows = Store.Values
            .Where(c => !c.IsDeleted && (!c.IsArchived || includeArchived))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<Card>> ListForListAsync(BoardListId listId, bool includeArchived, CancellationToken ct = default)
    {
        IReadOnlyList<Card> rows = Store.Values
            .Where(c => c.ListId.Value == listId.Value && (!c.IsArchived || includeArchived))
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);
}

/// <summary>In-memory <see cref="IBoardListRepository"/>.</summary>
public sealed class InMemoryBoardListRepository : InMemoryRepositoryBase<BoardList, BoardListId>, IBoardListRepository
{
    public Task<IReadOnlyList<BoardList>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default)
    {
        IReadOnlyList<BoardList> rows = Store.Values
            .Where(l => l.BoardId.Value == boardId.Value && (!l.IsArchived || includeArchived))
            .ToList();
        return Task.FromResult(rows);
    }
}

/// <summary>In-memory <see cref="ILabelRepository"/>.</summary>
public sealed class InMemoryLabelRepository : InMemoryRepositoryBase<Label, LabelId>, ILabelRepository
{
    public Task<IReadOnlyList<Label>> ListForBoardAsync(BoardId boardId, CancellationToken ct = default)
    {
        IReadOnlyList<Label> rows = Store.Values
            .Where(l => l.BoardId.Value == boardId.Value)
            .ToList();
        return Task.FromResult(rows);
    }
}
