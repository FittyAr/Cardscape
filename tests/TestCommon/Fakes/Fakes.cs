using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Recurrence;
using Cardscape.Domain.Voting;
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

/// <summary>Test double for <see cref="IDeploymentRegion"/>. Default
/// region is <see cref="Region.Unspecified"/> (no gating); tests
/// can set <see cref="Region"/> directly to assert the cross-region
/// write guard.</summary>
public sealed class FakeDeploymentRegion : IDeploymentRegion
{
    public Region Region { get; set; } = Region.Unspecified;
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

    public Task<IReadOnlyList<Card>> ListDueInRangeForBoardAsync(
        BoardId boardId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        IReadOnlyList<Card> rows = Store.Values
            .Where(c => c.DueDate is not null
                        && c.DueDate.Value >= from
                        && c.DueDate.Value < to)
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

    public Task<IReadOnlyDictionary<Guid, Guid>> ListBoardIdsByListIdAsync(CancellationToken ct = default)
    {
        IReadOnlyDictionary<Guid, Guid> map = Store.Values
            .ToDictionary(l => l.Id.Value, l => l.BoardId.Value);
        return Task.FromResult(map);
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

/// <summary>In-memory <see cref="IBoardExtensionRepository"/>.</summary>
public sealed class InMemoryBoardExtensionRepository
    : InMemoryRepositoryBase<BoardExtension, BoardExtensionId>, IBoardExtensionRepository
{
    public Task<IReadOnlyList<BoardExtension>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        IReadOnlyList<BoardExtension> rows = Store.Values
            .Where(e => e.BoardId.Value == boardId.Value)
            .OrderBy(e => (int)e.Kind)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<BoardExtension?> GetByBoardAndKindAsync(
        BoardId boardId, ExtensionKind kind, CancellationToken ct = default)
    {
        BoardExtension? match = Store.Values
            .FirstOrDefault(e => e.BoardId.Value == boardId.Value && e.Kind == kind);
        return Task.FromResult(match);
    }
}

/// <summary>In-memory <see cref="ICustomFieldDefinitionRepository"/>.</summary>
public sealed class InMemoryCustomFieldDefinitionRepository
    : InMemoryRepositoryBase<CustomFieldDefinition, CustomFieldDefinitionId>, ICustomFieldDefinitionRepository
{
    public Task<IReadOnlyList<CustomFieldDefinition>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        IReadOnlyList<CustomFieldDefinition> rows = Store.Values
            .Where(d => d.BoardId.Value == boardId.Value)
            .OrderBy(d => d.Position)
            .ToList();
        return Task.FromResult(rows);
    }
}

/// <summary>In-memory <see cref="ICustomFieldValueRepository"/>.</summary>
public sealed class InMemoryCustomFieldValueRepository
    : InMemoryRepositoryBase<CustomFieldValue, CustomFieldValueId>, ICustomFieldValueRepository
{
    private readonly InMemoryCustomFieldDefinitionRepository definitions;

    public InMemoryCustomFieldValueRepository(InMemoryCustomFieldDefinitionRepository definitions)
    {
        this.definitions = definitions;
    }

    public Task<IReadOnlyList<CustomFieldValue>> ListForCardAsync(
        CardId cardId, CancellationToken ct = default)
    {
        IReadOnlyList<CustomFieldValue> rows = Store.Values
            .Where(v => v.CardId.Value == cardId.Value)
            .ToList();
        return Task.FromResult(rows);
    }

    public async Task<IReadOnlyList<CustomFieldValue>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        IReadOnlyList<CustomFieldDefinition> fields =
            await definitions.ListForBoardAsync(boardId, ct);
        HashSet<Guid> fieldIds = fields.Select(f => f.Id.Value).ToHashSet();
        IReadOnlyList<CustomFieldValue> rows = Store.Values
            .Where(v => fieldIds.Contains(v.FieldDefinitionId.Value))
            .ToList();
        return rows;
    }

    public Task<CustomFieldValue?> GetByFieldAndCardAsync(
        CustomFieldDefinitionId fieldId, CardId cardId, CancellationToken ct = default)
    {
        CustomFieldValue? match = Store.Values
            .FirstOrDefault(v =>
                v.FieldDefinitionId.Value == fieldId.Value &&
                v.CardId.Value == cardId.Value);
        return Task.FromResult(match);
    }
}

/// <summary>In-memory <see cref="IActivityRepository"/>. Mirrors
/// the EF Core repo's cursor-paginated contract: filter by
/// board/card, sort by <c>OccurredAt</c> descending, then
/// <c>Id</c> descending as a tie-breaker, take the requested
/// limit.</summary>
public sealed class InMemoryActivityRepository
    : InMemoryRepositoryBase<Activity, ActivityId>, IActivityRepository
{
    public Task<IReadOnlyList<Activity>> ListForBoardAsync(
        BoardId boardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default)
    {
        IReadOnlyList<Activity> rows = Store.Values
            .Where(a => a.BoardId.Value == boardId.Value
                        && PassesCursor(a, beforeOccurredAt, beforeId))
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id.Value)
            .Take(limit)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<Activity>> ListForCardAsync(
        CardId cardId,
        int limit,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId,
        CancellationToken ct = default)
    {
        IReadOnlyList<Activity> rows = Store.Values
            .Where(a => a.CardId == cardId.Value
                        && PassesCursor(a, beforeOccurredAt, beforeId))
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id.Value)
            .Take(limit)
            .ToList();
        return Task.FromResult(rows);
    }

    private static bool PassesCursor(
        Activity a, DateTimeOffset? beforeOccurredAt, Guid? beforeId)
    {
        if (beforeOccurredAt is not { } cursorTime || beforeId is not { } cursorId)
        {
            return true;
        }

        if (a.OccurredAt < cursorTime)
        {
            return true;
        }

        if (a.OccurredAt > cursorTime)
        {
            return false;
        }

        return a.Id.Value.CompareTo(cursorId) < 0;
    }
}


public sealed class InMemoryCardVoteRepository
    : InMemoryRepositoryBase<CardVote, CardVoteId>, ICardVoteRepository
{
    public Task<int> CountForCardAsync(CardId cardId, CancellationToken ct = default) =>
        Task.FromResult(Store.Values.Count(v => v.CardId.Value == cardId.Value));

    public Task<bool> HasVotedAsync(CardId cardId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Store.Values.Any(v => v.CardId.Value == cardId.Value && v.UserId == userId));

    public Task<IReadOnlyList<CardVote>> ListForCardAsync(CardId cardId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CardVote>>(
            Store.Values.Where(v => v.CardId.Value == cardId.Value).OrderBy(v => v.VotedAt).ToList());
}


public sealed class InMemoryChecklistRepository
    : InMemoryRepositoryBase<Checklist, ChecklistId>, IChecklistRepository
{
    public Task<IReadOnlyList<Checklist>> ListForCardAsync(Guid cardId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Checklist>>(
            Store.Values.Where(c => c.CardId.Value == cardId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt).ToList());
}

public sealed class InMemoryChecklistItemRepository
    : InMemoryRepositoryBase<ChecklistItem, ChecklistItemId>, IChecklistItemRepository
{
    public Task<IReadOnlyList<ChecklistItem>> ListForChecklistAsync(Guid checklistId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChecklistItem>>(
            Store.Values.Where(i => i.ChecklistId.Value == checklistId)
                .OrderBy(i => i.Position.Value).ToList());
}


public sealed class InMemoryCardRecurrenceRepository
    : InMemoryRepositoryBase<CardRecurrence, CardRecurrenceId>, ICardRecurrenceRepository
{
    public Task<bool> ExistsForCardAsync(CardId cardId, CancellationToken ct = default) =>
        Task.FromResult(Store.Values.Any(r => r.CardId.Value == cardId.Value));

    public Task<CardRecurrence?> GetForCardAsync(CardId cardId, CancellationToken ct = default) =>
        Task.FromResult(Store.Values.FirstOrDefault(r => r.CardId.Value == cardId.Value));

    public Task<IReadOnlyList<CardRecurrence>> ListDueAsync(
        DateTimeOffset now, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CardRecurrence>>(
            Store.Values.Where(r => r.IsActive && r.NextOccurrenceAt <= now)
                .OrderBy(r => r.NextOccurrenceAt).Take(limit).ToList());
}
