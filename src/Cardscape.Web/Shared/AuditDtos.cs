namespace Cardscape.Web.Shared;

// ── Administration audit log ─────────────────────────────

/// <summary>Mirror of the API's AuditEntryDto. <see cref="Action"/> is a stable
/// code such as <c>workspace.member_role_changed</c>; roles in
/// <see cref="Details"/> are role names (<c>Admin</c>, <c>Member</c>, ...).</summary>
public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string ActorName,
    string Action,
    string TargetType,
    Guid? TargetId,
    string TargetName,
    Guid? WorkspaceId,
    string? WorkspaceName,
    Guid? BoardId,
    string? BoardName,
    IReadOnlyDictionary<string, string>? Details);

public sealed record AuditEntryPageDto(IReadOnlyList<AuditEntryDto> Items, int Total);

/// <summary>Filters of an audit-log listing; null means "any".</summary>
public sealed record AuditQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? ActionPrefix = null,
    string? Search = null,
    Guid? ActorUserId = null,
    Guid? TargetUserId = null);
