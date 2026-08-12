using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.Abstractions;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Authentication.Totp.Errors;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Recurrence;
using Cardscape.Domain.Voting;
using Cardscape.Domain.Workspaces;
using OtpNet;

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
/// as base64. Tracks issued tokens for
/// assertions.</summary>
public sealed class FakeTokenService : ITokenService
{
    public List<(User User, IReadOnlyCollection<string> Roles)> AccessTokensIssued { get; } = [];

    public string IssueAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        AccessTokensIssued.Add((user, roles));
        return Convert.ToBase64String(user.Id.Value.ToByteArray());
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
    public Dictionary<string, string> ExtraClaims { get; } = new();

    public string? FindFirst(string claimType) =>
        ExtraClaims.TryGetValue(claimType, out string? value) ? value : null;

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

    public Task<IReadOnlyList<User>> ListByIdsAsync(
        IReadOnlyList<UserId> ids, CancellationToken ct = default)
    {
        HashSet<UserId> idSet = new(ids);
        IReadOnlyList<User> matches = Store.Values
            .Where(u => idSet.Contains(u.Id))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<WorkspaceMember>> ListWorkspaceMembersAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        // The in-memory workspace list is held on the
        // WorkspaceRepository; for the SCIM tests we just
        // look up the workspace and return its member
        // collection. Tests that need full coverage seed
        // both repositories.
        IReadOnlyList<WorkspaceMember> empty = [];
        return Task.FromResult(empty);
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

    // BETA-3-#3 — see test-results/BETA-TEST-REPORT.md. The
    // in-memory fake mirrors the production behaviour: toggle
    // the star directly on the aggregate so we don't go through
    // the Board's RowVersion. The aggregate's Star/Unstar methods
    // already do an existence check so the "missing/already there"
    // distinction is preserved.
    public Task<bool> AddStarIfMissingAsync(
        BoardId boardId, Guid userId, DateTimeOffset at, CancellationToken ct = default)
    {
        var board = Store.Values.FirstOrDefault(b => b.Id.Value == boardId.Value);
        if (board is null)
        {
            return Task.FromResult(false);
        }

        if (board.IsStarredBy(userId))
        {
            return Task.FromResult(false);
        }

        board.Star(userId, at);
        return Task.FromResult(true);
    }

    public Task<bool> RemoveStarIfPresentAsync(
        BoardId boardId, Guid userId, CancellationToken ct = default)
    {
        var board = Store.Values.FirstOrDefault(b => b.Id.Value == boardId.Value);
        if (board is null)
        {
            return Task.FromResult(false);
        }

        if (!board.IsStarredBy(userId))
        {
            return Task.FromResult(false);
        }

        board.Unstar(userId, at: DateTimeOffset.UtcNow);
        return Task.FromResult(true);
    }
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

    // BUG-A6-007 — the in-memory fakes must implement the new
    // list-name lookup the planner now uses (see
    // IBoardListRepository.ListNamesByIdAsync). Mirrors the
    // SQL-backed implementation in BoardListRepository.
    public Task<IReadOnlyDictionary<Guid, string>> ListNamesByIdAsync(CancellationToken ct = default)
    {
        IReadOnlyDictionary<Guid, string> map = Store.Values
            .ToDictionary(l => l.Id.Value, l => l.Name.Value);
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

    // BETA-3-#2 — see test-results/BETA-TEST-REPORT.md. The in-memory
    // fake mirrors the production ToggleAsync: delete-then-insert in
    // a single step (in-memory transactions are implicit), then
    // re-read state for the response DTO.
    public Task<VoteToggleResult> ToggleAsync(
        CardId cardId, Guid userId, DateTimeOffset at, CancellationToken ct = default)
    {
        var existing = Store.Values
            .FirstOrDefault(v => v.CardId.Value == cardId.Value && v.UserId == userId);
        bool nowVoted;
        if (existing is not null)
        {
            Store.Remove(existing.Id);
            nowVoted = false;
        }
        else
        {
            var createResult = CardVote.Create(new CardVoteId(Guid.NewGuid()), cardId, userId, at);
            if (createResult.IsSuccess)
            {
                Store[createResult.Value.Id] = createResult.Value;
                nowVoted = true;
            }
            else
            {
                nowVoted = false;
            }
        }

        int count = Store.Values.Count(v => v.CardId.Value == cardId.Value);
        return Task.FromResult(new VoteToggleResult(nowVoted, count));
    }
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

/// <summary>
/// In-memory <see cref="ITotpCredentialRepository"/>. The
/// <c>EncryptedSecret</c> column is treated as the cleartext
/// base32 secret because the <see cref="IdentitySecretProtector"/>
/// test double below is the identity function. The default
/// <see cref="ITotpService"/> test double (FakeTotpService)
/// uses the same convention.
/// </summary>
public sealed class InMemoryTotpCredentialRepository
    : InMemoryRepositoryBase<TotpCredential, TotpCredentialId>, ITotpCredentialRepository
{
    public Task<TotpCredential?> FindForUserAsync(UserId userId, CancellationToken ct = default) =>
        Task.FromResult(Store.Values.FirstOrDefault(c => c.UserId == userId));
}

/// <summary>
/// In-memory <see cref="IIdempotencyKeyStore"/>. The dictionary
/// is keyed by <c>(OwnerId, Key.Value)</c> so two retries of
/// the same logical request from the same user collapse to a
/// single entry, matching the EF Core unique index in
/// production. Safe for single-threaded tests.
/// </summary>
public sealed class InMemoryIdempotencyKeyStore : IIdempotencyKeyStore
{
    private readonly Dictionary<(UserId, string), IdempotencyKey> _store = [];
    private readonly object _gate = new();

    public int Count { get { lock (_gate) return _store.Count; } }

    public IReadOnlyCollection<IdempotencyKey> All
    {
        get { lock (_gate) return _store.Values.ToArray(); }
    }

    public Task<IdempotencyKey?> FindAsync(
        UserId ownerId,
        IdempotencyKeyValue key,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            _store.TryGetValue((ownerId, key.Value), out var existing);
            return Task.FromResult(existing);
        }
    }

    public Task<bool> TryReserveAsync(
        IdempotencyKey reservation,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var key = (reservation.OwnerId, reservation.Key.Value);
            if (_store.ContainsKey(key)) return Task.FromResult(false);
            _store.Add(key, reservation);
            return Task.FromResult(true);
        }
    }

    public Task<bool> CompleteReservationAsync(
        IdempotencyKeyId id,
        int responseStatusCode,
        string responseJson,
        DateTimeOffset completedAt,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            KeyValuePair<(UserId, string), IdempotencyKey> entry = _store
                .SingleOrDefault(pair => pair.Value.Id == id && pair.Value.IsPending);
            if (entry.Value is null) return Task.FromResult(false);
            Result completed = entry.Value.Complete(
                responseStatusCode, responseJson, completedAt);
            return Task.FromResult(completed.IsSuccess);
        }
    }

    public Task ReleaseAsync(IdempotencyKeyId id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            (UserId, string)? key = _store
                .Where(pair => pair.Value.Id == id)
                .Select(pair => ((UserId, string)?)pair.Key)
                .SingleOrDefault();
            if (key is not null) _store.Remove(key.Value);
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Identity-function <see cref="ISecretProtector"/>. The TOTP
/// secret in the fake repository is stored as the cleartext
/// base32 string; the service unprotects it back to the same
/// string. Production wires <c>DataProtectionSecretProtector</c>.
/// </summary>
public sealed class IdentitySecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string protectedValue) => protectedValue;
}

/// <summary>
/// Test double for <see cref="ITotpService"/>. The "secret" used
/// for verification is the cleartext stored in the credential's
/// <c>EncryptedSecret</c> column (the fake protector is the
/// identity). Verifying advances the credential's
/// <see cref="TotpCredential.LastUsedCounter"/> exactly the way
/// the production service does.
/// </summary>
public sealed class FakeTotpService(
    ITotpCredentialRepository credentials,
    ISecretProtector protector,
    IClock clock,
    IUnitOfWork unitOfWork) : ITotpService
{
    public async Task<Result<TotpEnrollment>> EnrollAsync(UserId userId, CancellationToken ct)
    {
        var existing = await credentials.FindForUserAsync(userId, ct);
        if (existing?.IsActive == true)
        {
            return Result.Failure<TotpEnrollment>(TotpErrors.AlreadyEnrolled);
        }

        if (existing is not null)
        {
            credentials.Remove(existing);
            await unitOfWork.SaveChangesAsync(ct);
        }

        byte[] secretBytes = KeyGeneration.GenerateRandomKey(20);
        string base32Secret = Base32Encoding.ToString(secretBytes);

        var recoveryCodes = new List<string>(TotpCredential.RecoveryCodeCount);
        var hashedLines = new List<string>(TotpCredential.RecoveryCodeCount);
        for (int i = 0; i < TotpCredential.RecoveryCodeCount; i++)
        {
            byte[] codeBytes = RandomNumberGenerator.GetBytes(TotpCredential.RecoveryCodeLength);
            recoveryCodes.Add(Convert.ToBase64String(codeBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_')
                [..TotpCredential.RecoveryCodeLength]);
            hashedLines.Add(Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(recoveryCodes[^1]))).ToLowerInvariant());
        }

        var enrollResult = TotpCredential.Enroll(
            userId, protector.Protect(base32Secret),
            string.Join('\n', hashedLines), clock.UtcNow);
        if (enrollResult.IsFailure)
        {
            return Result.Failure<TotpEnrollment>(enrollResult.Error);
        }

        await credentials.AddAsync(enrollResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        string accountLabel = Uri.EscapeDataString(userId.Value.ToString());
        string otpauth = $"otpauth://totp/Cardscape:{accountLabel}?secret={base32Secret}&issuer=Cardscape";
        return Result.Success(new TotpEnrollment(
            enrollResult.Value.Id, base32Secret, otpauth, recoveryCodes));
    }

    public async Task<Result> ConfirmEnrollmentAsync(UserId userId, string code, CancellationToken ct)
    {
        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential is null || credential.IsDeleted || credential.ConfirmedAt.HasValue)
        {
            return Result.Failure(TotpErrors.NotPendingEnrollment);
        }

        string cleartextSecret = protector.Unprotect(credential.EncryptedSecret);
        var totp = new Totp(Base32Encoding.ToBytes(cleartextSecret));
        if (string.IsNullOrWhiteSpace(code)
            || !totp.VerifyTotp(code.Trim(), out long matchedStep, VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            return Result.Failure(TotpErrors.InvalidCode);
        }

        credential.Confirm(matchedStep, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<long>> VerifyAsync(UserId userId, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length is < 6 or > 10)
        {
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }

        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential?.IsActive != true)
        {
            return Result.Failure<long>(TotpErrors.NotEnrolled);
        }

        string cleartextSecret = protector.Unprotect(credential.EncryptedSecret);
        var totp = new Totp(Base32Encoding.ToBytes(cleartextSecret));

        if (!totp.VerifyTotp(code.Trim(), out long matchedStep, VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }

        if (matchedStep <= credential.LastUsedCounter)
        {
            return Result.Failure<long>(TotpErrors.InvalidCode);
        }

        credential.RecordVerification(matchedStep, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(matchedStep);
    }

    public async Task<Result> ConsumeRecoveryCodeAsync(UserId userId, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(TotpErrors.InvalidRecoveryCode);
        }

        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential?.IsActive != true)
        {
            return Result.Failure(TotpErrors.NotEnrolled);
        }

        string submittedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()))).ToLowerInvariant();
        var lines = credential.RecoveryCodesHash
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        int matchIndex = lines.FindIndex(l => string.Equals(l, submittedHash, StringComparison.Ordinal));
        if (matchIndex < 0)
        {
            return Result.Failure(TotpErrors.InvalidRecoveryCode);
        }

        lines[matchIndex] = $"used:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        credential.RecordRecoveryCodeUsed(string.Join('\n', lines), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DisableAsync(UserId userId, string code, CancellationToken ct)
    {
        var totpResult = await VerifyAsync(userId, code, ct);
        if (totpResult.IsSuccess)
        {
            var cred = await credentials.FindForUserAsync(userId, ct);
            cred?.Disable(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }

        var recoveryResult = await ConsumeRecoveryCodeAsync(userId, code, ct);
        if (recoveryResult.IsSuccess)
        {
            var cred = await credentials.FindForUserAsync(userId, ct);
            cred?.Disable(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }

        return Result.Failure(TotpErrors.InvalidCode);
    }

    public async Task<TotpStatus> GetStatusAsync(UserId userId, CancellationToken ct)
    {
        var credential = await credentials.FindForUserAsync(userId, ct);
        if (credential is null || credential.IsDeleted)
        {
            return new TotpStatus(false, false, null, 0);
        }

        if (!credential.IsActive)
        {
            return new TotpStatus(false, true, null, 0);
        }

        int remaining = credential.RecoveryCodesHash
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(l => !l.StartsWith("used:", StringComparison.Ordinal));
        return new TotpStatus(true, false, credential.ConfirmedAt, remaining);
    }
}

/// <summary>
/// In-memory <see cref="IPendingTotpLoginStore"/>. Mirrors the
/// production behaviour: <c>Mint</c> produces a 32-byte
/// base64 token bound to a <see cref="UserId"/> with a 5-minute
/// TTL; <c>Consume</c> is one-shot (TryRemove) and returns
/// <c>null</c> for unknown / expired / already-consumed tokens.
/// </summary>
public sealed class InMemoryPendingTotpLoginStore : IPendingTotpLoginStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (UserId UserId, DateTimeOffset ExpiresAt)> _entries = new(System.StringComparer.Ordinal);

    public string Mint(UserId userId)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToBase64String(bytes);
        _entries[token] = (userId, DateTimeOffset.UtcNow.AddMinutes(5));
        return token;
    }

    public UserId? Consume(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }
        if (!_entries.TryRemove(token, out var entry))
        {
            return null;
        }
        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }
        return entry.UserId;
    }
}

/// <summary>In-memory <see cref="IGoogleCalendarConnectionRepository"/>.</summary>
public sealed class InMemoryGoogleCalendarConnectionRepository : IGoogleCalendarConnectionRepository
{
    private readonly Dictionary<GoogleCalendarConnectionId, GoogleCalendarConnection> byId = [];
    private readonly Dictionary<Guid, GoogleCalendarConnection> byUser = [];

    public IReadOnlyCollection<GoogleCalendarConnection> All => byId.Values.ToList();

    public Task<GoogleCalendarConnection?> FindByUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(byUser.GetValueOrDefault(userId));

    public Task<GoogleCalendarConnection?> FindByIdAsync(GoogleCalendarConnectionId id, CancellationToken ct = default) =>
        Task.FromResult(byId.GetValueOrDefault(id));

    public Task AddAsync(GoogleCalendarConnection connection, CancellationToken ct = default)
    {
        byId[connection.Id] = connection;
        byUser[connection.UserId] = connection;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GoogleCalendarConnection>> ListActiveForWorkspaceAsync(
        WorkspaceId workspaceId, CancellationToken ct = default)
    {
        IReadOnlyList<GoogleCalendarConnection> rows = byId.Values
            .Where(c => c.WorkspaceId == workspaceId && c.IsActive)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task UpdateAsync(GoogleCalendarConnection connection, CancellationToken ct = default)
    {
        byId[connection.Id] = connection;
        byUser[connection.UserId] = connection;
        return Task.CompletedTask;
    }
}

/// <summary>Stub <see cref="IGoogleCalendarSyncService"/>.</summary>
public sealed class FakeGoogleCalendarSyncService : IGoogleCalendarSyncService
{
    public List<(Guid UserId, Guid CardId, string CardTitle, string? CardDescription, DateTimeOffset? DueDate)> PushCalls { get; } = [];
    public Result<string>? NextPushResult { get; set; }

    public Task<Result<string>> PushCardDueDateAsync(
        Guid userId, Guid cardId, string cardTitle, string? cardDescription,
        DateTimeOffset? dueDate, CancellationToken ct = default)
    {
        PushCalls.Add((userId, cardId, cardTitle, cardDescription, dueDate));
        return Task.FromResult(NextPushResult ?? Result.Success(string.Empty));
    }
}

/// <summary>BETA-7-#1 — see test-results/BETA-TEST-REPORT.md. No-op in-memory ISearchIndex for the test suite. The production InMemorySearchIndex lives in Cardscape.Infrastructure; we keep this fake here so the test projects can resolve the new dependency without pulling in EF / Infrastructure.</summary>
public sealed class FakeSearchIndex : ISearchIndex
{
    public List<Guid> IndexedCardIds { get; } = [];
    public List<Guid> IndexedCommentIds { get; } = [];
    public List<Guid> RemovedCardIds { get; } = [];

    public Task IndexCardAsync(Card card, Guid boardId, CancellationToken ct = default)
    {
        IndexedCardIds.Add(card.Id.Value);
        return Task.CompletedTask;
    }

    public Task RemoveCardAsync(Guid cardId, CancellationToken ct = default)
    {
        RemovedCardIds.Add(cardId);
        return Task.CompletedTask;
    }

    public Task IndexCommentAsync(Comment comment, Guid boardId, CancellationToken ct = default)
    {
        IndexedCommentIds.Add(comment.Id.Value);
        return Task.CompletedTask;
    }

    public Task IndexChecklistItemAsync(ChecklistItem item, Checklist checklist, Guid boardId, CancellationToken ct = default) => Task.CompletedTask;

    public Task IndexLabelAsync(Label label, CancellationToken ct = default) => Task.CompletedTask;

    public Task IndexActivityAsync(Activity activity, CancellationToken ct = default) => Task.CompletedTask;

    public Task<SearchPage> SearchAsync(string query, Guid? boardId, SearchHitKind? kind, int page, int pageSize, IReadOnlySet<Guid> allowedBoardIds, CancellationToken ct = default) =>
        Task.FromResult(new SearchPage([], 0));
}


public sealed class InMemoryCommentRepository
    : InMemoryRepositoryBase<Comment, CommentId>, ICommentRepository
{
    public Task<IReadOnlyList<Comment>> ListForCardAsync(CardId cardId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            Store.Values.Where(c => c.CardId.Value == cardId.Value).OrderBy(c => c.CreatedAt).ToList());
}
