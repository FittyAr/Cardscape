using System.Runtime.CompilerServices;
using System.Text.Json;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Audit;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Members.Events;
using Cardscape.Domain.Workspaces;
using Cardscape.Domain.Workspaces.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Infrastructure.Persistence.Audit;

/// <summary>
/// Turns the membership and account-administration domain events of a
/// save into <see cref="AuditEntry"/> rows of the same save, so an audit
/// line exists exactly when the change it describes was committed. Runs
/// inside <c>SavingChangesAsync</c> (see <c>DomainEventsInterceptor</c>):
/// the actor is the signed-in caller of the current request, if any; the
/// names of everyone involved are snapshotted now. When a user is
/// anonymised, the same save also scrubs that person's name (and their
/// former email on invitation lines) from every earlier entry.
/// </summary>
internal sealed class AuditTrailWriter(IServiceProvider services)
{
    private static readonly JsonSerializerOptions DetailsJson = new(JsonSerializerDefaults.Web);

    // Events stay on the aggregate until the save succeeds; a retried save
    // (e.g. after a concurrency conflict) must not log them twice.
    private readonly ConditionalWeakTable<IDomainEvent, object> _recorded = [];

    public async Task RecordAsync(DbContext context, CancellationToken cancellationToken)
    {
        List<AuditDraft> drafts = [];
        List<(Guid UserId, string? FormerEmail)> anonymised = [];
        foreach (EntityEntry entry in context.ChangeTracker.Entries().Where(e => e.Entity is IAggregateRoot).ToList())
        {
            foreach (IDomainEvent domainEvent in ((IAggregateRoot)entry.Entity).DomainEvents)
            {
                if (_recorded.TryGetValue(domainEvent, out _))
                {
                    continue;
                }

                AuditDraft? draft = AuditEventMapper.Map(domainEvent, FactsFor(context, entry, domainEvent));
                if (draft is null)
                {
                    continue;
                }

                _recorded.Add(domainEvent, draft);
                drafts.Add(draft);
                if (domainEvent is UserAnonymised gone)
                {
                    anonymised.Add((gone.UserId.Value, FormerEmail(entry)));
                }
            }
        }

        if (anonymised.Count > 0)
        {
            await ScrubAsync(context, anonymised, cancellationToken);
        }

        if (drafts.Count == 0)
        {
            return;
        }

        IReadOnlyList<AuditDraft> collapsed = AuditEventMapper.Collapse(drafts);
        (Guid? actorId, string actorName) = await ResolveRequestActorAsync(context, cancellationToken);
        Names names = await Names.ResolveAsync(context, collapsed, cancellationToken);

        long lastTicks = 0;
        foreach (AuditDraft draft in collapsed)
        {
            Guid? entryActorId = actorId;
            string entryActorName = actorName;
            if (entryActorId is null && actorName == AuditEntry.SystemActorName
                && draft.FallbackActorId is { } fallback && names.Users.TryGetValue(fallback, out string? fallbackName))
            {
                entryActorId = fallback;
                entryActorName = fallbackName;
            }

            Guid? workspaceId = draft.WorkspaceId;
            string? boardName = null;
            if (draft.BoardId is { } boardId && names.Boards.TryGetValue(boardId, out (string Name, Guid WorkspaceId) board))
            {
                boardName = board.Name;
                workspaceId ??= board.WorkspaceId;
            }

            string? workspaceName = workspaceId is { } wsId ? names.Workspaces.GetValueOrDefault(wsId) : null;
            string targetName = draft.TargetName
                ?? names.Users.GetValueOrDefault(draft.TargetId)
                ?? string.Empty;

            // Keep the raise order of one save visible: entries of the same
            // instant get consecutive ticks.
            long ticks = Math.Max(draft.OccurredAt.UtcTicks, lastTicks + 1);
            lastTicks = ticks;

            context.Add(AuditEntry.Create(
                new DateTimeOffset(ticks, TimeSpan.Zero),
                entryActorId,
                entryActorName,
                draft.Action,
                draft.TargetType,
                draft.TargetId,
                targetName,
                workspaceId,
                workspaceName,
                draft.BoardId,
                boardName,
                SerializeDetails(draft, names)));
        }
    }

    private static AuditEventFacts? FactsFor(DbContext context, EntityEntry source, IDomainEvent domainEvent) =>
        domainEvent switch
        {
            WorkspaceMemberRoleChanged e => new AuditEventFacts(PreviousRole: PreviousWorkspaceRole(context, e.WorkspaceId, e.UserId)),
            WorkspaceMemberRemoved e => new AuditEventFacts(PreviousRole: PreviousWorkspaceRole(context, e.WorkspaceId, e.UserId)),
            BoardMemberRoleChanged e => new AuditEventFacts(PreviousRole: PreviousBoardRole(context, e.BoardId, e.UserId)),
            BoardMemberRemoved e => new AuditEventFacts(PreviousRole: PreviousBoardRole(context, e.BoardId, e.UserId)),
            WorkspaceRenamed when source.Entity is Workspace && source.State != EntityState.Added =>
                new AuditEventFacts(PreviousName: source.OriginalValues.GetValue<WorkspaceName>(nameof(Workspace.Name))?.Value),
            WorkspaceArchived or WorkspaceUnarchived or WorkspaceDeleted or WorkspaceRegionChanged
                or WorkspaceTwoFactorRequirementChanged
                when source.Entity is Workspace workspace =>
                new AuditEventFacts(WorkspaceName: workspace.Name.Value),
            WorkspaceInvitationIssued or WorkspaceInvitationRevoked or WorkspaceInvitationAccepted
                when source.Entity is WorkspaceInvitation invitation =>
                new AuditEventFacts(InvitationEmail: invitation.Email, InvitationRole: invitation.Role.ToString()),
            _ => null
        };

    // The original value is the role before this save: the change tracker
    // still holds it for a modified or deleted membership row.
    private static string? PreviousWorkspaceRole(DbContext context, WorkspaceId workspaceId, Guid userId) =>
        context.ChangeTracker.Entries<WorkspaceMember>()
            .Where(m => m.Entity.UserId == userId && m.Entity.WorkspaceId == workspaceId)
            .Select(m => m.OriginalValues.GetValue<WorkspaceRole>(nameof(WorkspaceMember.Role)).ToString())
            .FirstOrDefault();

    private static string? PreviousBoardRole(DbContext context, BoardId boardId, Guid userId) =>
        context.ChangeTracker.Entries<BoardMember>()
            .Where(m => m.Entity.UserId == userId && m.Entity.BoardId == boardId)
            .Select(m => m.OriginalValues.GetValue<BoardMemberRole>(nameof(BoardMember.Role)).ToString())
            .FirstOrDefault();

    private static string? FormerEmail(EntityEntry entry) =>
        entry.Entity is User && entry.State != EntityState.Added
            ? entry.OriginalValues.GetValue<EmailAddress>(nameof(User.Email))?.Value
            : null;

    private async Task<(Guid? Id, string Name)> ResolveRequestActorAsync(DbContext context, CancellationToken cancellationToken)
    {
        ICurrentUser? currentUser = services.GetService<ICurrentUser>();
        if (currentUser is null || !currentUser.IsAuthenticated)
        {
            return (null, AuditEntry.SystemActorName);
        }

        // A SCIM bearer identifies the identity provider's token, not a person.
        if (currentUser.FindFirst("scim.workspace_id") is not null)
        {
            return (null, AuditEntry.ScimActorName);
        }

        if (currentUser.Id is not { } id)
        {
            return (null, AuditEntry.SystemActorName);
        }

        string? name = context.ChangeTracker.Entries<User>()
            .Where(u => u.Entity.Id == id)
            .Select(u => u.Entity.DisplayName.Value)
            .FirstOrDefault()
            ?? await context.Set<User>().AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => u.DisplayName)
                .Select(d => d.Value)
                .FirstOrDefaultAsync(cancellationToken);
        return name is null ? (null, AuditEntry.SystemActorName) : (id.Value, name);
    }

    private static string? SerializeDetails(AuditDraft draft, Names names)
    {
        Dictionary<string, string> details = draft.Details is null
            ? new(StringComparer.Ordinal)
            : new(draft.Details, StringComparer.Ordinal);
        foreach ((string key, Guid userId) in draft.NamedUsers ?? new Dictionary<string, Guid>())
        {
            details[key + "Id"] = userId.ToString();
            details[key + "Name"] = names.Users.GetValueOrDefault(userId) ?? string.Empty;
        }

        if (details.Count == 0)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(details, DetailsJson);
        return json.Length <= AuditEntry.DetailsMaxLength ? json : null;
    }

    private static async Task ScrubAsync(
        DbContext context, List<(Guid UserId, string? FormerEmail)> anonymised, CancellationToken cancellationToken)
    {
        foreach ((Guid userId, string? formerEmail) in anonymised)
        {
            Guid? id = userId;
            string idText = userId.ToString();
            // Invitation emails are stored normalised (lower case).
            string? email = formerEmail?.ToLowerInvariant();
            List<AuditEntry> rows = await context.Set<AuditEntry>()
                .Where(a => a.ActorUserId == id
                    || a.TargetId == id
                    || (email != null && a.TargetType == AuditTargetTypes.Invitation && a.TargetName == email)
                    || (a.Details != null && a.Details.Contains(idText)))
                .ToListAsync(cancellationToken);
            foreach (AuditEntry row in rows)
            {
                row.ScrubUser(userId, User.AnonymisedDisplayName, formerEmail);
            }
        }

        context.ChangeTracker.DetectChanges();
    }

    /// <summary>Snapshot names of the people, workspaces and boards a batch mentions.</summary>
    private sealed record Names(
        IReadOnlyDictionary<Guid, string> Users,
        IReadOnlyDictionary<Guid, string> Workspaces,
        IReadOnlyDictionary<Guid, (string Name, Guid WorkspaceId)> Boards)
    {
        public static async Task<Names> ResolveAsync(
            DbContext context, IReadOnlyList<AuditDraft> drafts, CancellationToken cancellationToken)
        {
            HashSet<Guid> userIds = drafts
                .Where(d => d.TargetType == AuditTargetTypes.User)
                .Select(d => d.TargetId)
                .Concat(drafts.SelectMany(d => d.NamedUsers?.Values ?? []))
                .Concat(drafts.Where(d => d.FallbackActorId is not null).Select(d => d.FallbackActorId!.Value))
                .ToHashSet();

            Dictionary<Guid, string> users = [];
            foreach (EntityEntry<User> tracked in context.ChangeTracker.Entries<User>())
            {
                if (userIds.Contains(tracked.Entity.Id.Value))
                {
                    users[tracked.Entity.Id.Value] = tracked.Entity.DisplayName.Value;
                }
            }

            List<UserId> missingUsers = userIds.Where(id => !users.ContainsKey(id)).Select(id => new UserId(id)).ToList();
            if (missingUsers.Count > 0)
            {
                var rows = await context.Set<User>().AsNoTracking()
                    .Where(u => missingUsers.Contains(u.Id))
                    .Select(u => new { u.Id, u.DisplayName })
                    .ToListAsync(cancellationToken);
                foreach (var row in rows)
                {
                    users[row.Id.Value] = row.DisplayName.Value;
                }
            }

            Dictionary<Guid, (string Name, Guid WorkspaceId)> boards = [];
            HashSet<Guid> boardIds = drafts.Where(d => d.BoardId is not null).Select(d => d.BoardId!.Value).ToHashSet();
            foreach (EntityEntry<Board> tracked in context.ChangeTracker.Entries<Board>())
            {
                if (boardIds.Contains(tracked.Entity.Id.Value))
                {
                    boards[tracked.Entity.Id.Value] = (tracked.Entity.Name.Value, tracked.Entity.WorkspaceId.Value);
                }
            }

            List<BoardId> missingBoards = boardIds.Where(id => !boards.ContainsKey(id)).Select(id => new BoardId(id)).ToList();
            if (missingBoards.Count > 0)
            {
                var rows = await context.Set<Board>().AsNoTracking()
                    .Where(b => missingBoards.Contains(b.Id))
                    .Select(b => new { b.Id, b.Name, b.WorkspaceId })
                    .ToListAsync(cancellationToken);
                foreach (var row in rows)
                {
                    boards[row.Id.Value] = (row.Name.Value, row.WorkspaceId.Value);
                }
            }

            HashSet<Guid> workspaceIds = drafts.Where(d => d.WorkspaceId is not null).Select(d => d.WorkspaceId!.Value)
                .Concat(boards.Values.Select(b => b.WorkspaceId))
                .ToHashSet();
            Dictionary<Guid, string> workspaces = [];
            foreach (EntityEntry<Workspace> tracked in context.ChangeTracker.Entries<Workspace>())
            {
                if (workspaceIds.Contains(tracked.Entity.Id.Value))
                {
                    workspaces[tracked.Entity.Id.Value] = tracked.Entity.Name.Value;
                }
            }

            List<WorkspaceId> missingWorkspaces = workspaceIds.Where(id => !workspaces.ContainsKey(id))
                .Select(id => new WorkspaceId(id)).ToList();
            if (missingWorkspaces.Count > 0)
            {
                var rows = await context.Set<Workspace>().AsNoTracking()
                    .Where(w => missingWorkspaces.Contains(w.Id))
                    .Select(w => new { w.Id, w.Name })
                    .ToListAsync(cancellationToken);
                foreach (var row in rows)
                {
                    workspaces[row.Id.Value] = row.Name.Value;
                }
            }

            return new Names(users, workspaces, boards);
        }
    }
}
