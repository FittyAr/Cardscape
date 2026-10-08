using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cardscape.Domain.Audit;

/// <summary>
/// One row of the append-only administration audit log: who did what to
/// whom, and where. Names are snapshots taken when the entry is written,
/// so the log stays readable after the people or workspaces involved are
/// renamed or deleted. The only mutation allowed after creation is the
/// GDPR scrub (<see cref="ScrubUser"/>), which replaces an anonymised
/// person's name with the anonymised placeholder.
/// </summary>
public sealed class AuditEntry
{
    public const int ActionMaxLength = 64;
    public const int TargetTypeMaxLength = 32;
    public const int NameMaxLength = 320;
    public const int DetailsMaxLength = 2048;

    /// <summary>Stored actor name when nobody signed in did it (background jobs, tests, the seeder).</summary>
    public const string SystemActorName = "system";

    /// <summary>Stored actor name when a SCIM identity provider did it.</summary>
    public const string ScimActorName = "scim";

    public Guid Id { get; private set; }

    /// <summary>UTC ticks of <see cref="OccurredAt"/>. Stored as an integer so every
    /// provider (SQLite included) can order and range-filter it in SQL.</summary>
    public long OccurredAtUtcTicks { get; private set; }

    public DateTimeOffset OccurredAt => new(OccurredAtUtcTicks, TimeSpan.Zero);

    /// <summary>The signed-in user who performed the action; null for the system.</summary>
    public Guid? ActorUserId { get; private set; }

    public string ActorName { get; private set; } = string.Empty;

    /// <summary>Stable action code, e.g. <c>workspace.member_role_changed</c> (see <see cref="AuditActions"/>).</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary><c>user</c> or <c>invitation</c> (see <see cref="AuditTargetTypes"/>).</summary>
    public string TargetType { get; private set; } = string.Empty;

    public Guid? TargetId { get; private set; }

    public string TargetName { get; private set; } = string.Empty;

    public Guid? WorkspaceId { get; private set; }

    public string? WorkspaceName { get; private set; }

    public Guid? BoardId { get; private set; }

    public string? BoardName { get; private set; }

    /// <summary>Small JSON object with action-specific facts, e.g. <c>{"from":"Member","to":"Admin"}</c>.</summary>
    public string? Details { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Create(
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        string actorName,
        string action,
        string targetType,
        Guid? targetId,
        string targetName,
        Guid? workspaceId = null,
        string? workspaceName = null,
        Guid? boardId = null,
        string? boardName = null,
        string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        if (details is { Length: > DetailsMaxLength })
        {
            throw new ArgumentException($"Audit details must be at most {DetailsMaxLength} characters.", nameof(details));
        }

        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            OccurredAtUtcTicks = occurredAt.UtcTicks,
            ActorUserId = actorUserId,
            ActorName = Clip(actorName),
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = Clip(targetName),
            WorkspaceId = workspaceId,
            WorkspaceName = workspaceName is null ? null : Clip(workspaceName),
            BoardId = boardId,
            BoardName = boardName is null ? null : Clip(boardName),
            Details = details
        };
    }

    /// <summary>
    /// GDPR scrub: replaces the snapshot of <paramref name="userId"/>'s name
    /// (as actor or as target) with <paramref name="placeholder"/>, and an
    /// invitation addressed to <paramref name="formerEmail"/> with the
    /// placeholder too. Returns true when anything changed.
    /// </summary>
    public bool ScrubUser(Guid userId, string placeholder, string? formerEmail)
    {
        bool changed = false;
        if (ActorUserId == userId && ActorName != placeholder)
        {
            ActorName = placeholder;
            changed = true;
        }

        if (TargetType == AuditTargetTypes.User && TargetId == userId && TargetName != placeholder)
        {
            TargetName = placeholder;
            changed = true;
        }

        if (TargetType == AuditTargetTypes.Invitation
            && formerEmail is not null
            && string.Equals(TargetName, formerEmail, StringComparison.OrdinalIgnoreCase))
        {
            TargetName = placeholder;
            changed = true;
        }

        if (Details is not null && Details.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            changed |= ScrubDetails(userId, placeholder);
        }

        return changed;
    }

    // Details name other people as an "<x>Id"/"<x>Name" pair (for
    // example previousOwnerId/previousOwnerName); scrub the name next
    // to the anonymised user's id.
    private bool ScrubDetails(Guid userId, string placeholder)
    {
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(Details!) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        if (json is null)
        {
            return false;
        }

        bool changed = false;
        foreach (KeyValuePair<string, JsonNode?> property in json.ToList())
        {
            if (!property.Key.EndsWith("Id", StringComparison.Ordinal)
                || property.Value?.GetValueKind() != JsonValueKind.String
                || !Guid.TryParse(property.Value.GetValue<string>(), out Guid id)
                || id != userId)
            {
                continue;
            }

            string nameKey = property.Key[..^2] + "Name";
            if (json.ContainsKey(nameKey) && json[nameKey]?.ToString() != placeholder)
            {
                json[nameKey] = placeholder;
                changed = true;
            }
        }

        if (changed)
        {
            Details = json.ToJsonString();
        }

        return changed;
    }

    private static string Clip(string value) =>
        value.Length <= NameMaxLength ? value : value[..NameMaxLength];
}
