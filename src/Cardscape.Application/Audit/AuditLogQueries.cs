using System.Text.Json;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces;
using Cardscape.Domain.Audit;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Audit;

/// <summary>One line of the administration audit log.</summary>
/// <param name="ActorUserId">Null when the system (a background job, SCIM) did it.</param>
/// <param name="ActorName">Snapshot; <c>system</c> or <c>scim</c> when <paramref name="ActorUserId"/> is null.</param>
/// <param name="Action">Stable code, see <see cref="AuditActions"/>.</param>
/// <param name="Details">Action-specific facts, e.g. <c>from</c>/<c>to</c> roles.</param>
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
    IReadOnlyDictionary<string, string> Details)
{
    public static AuditEntryDto From(AuditEntry entry) => new(
        entry.Id,
        entry.OccurredAt,
        entry.ActorUserId,
        entry.ActorName,
        entry.Action,
        entry.TargetType,
        entry.TargetId,
        entry.TargetName,
        entry.WorkspaceId,
        entry.WorkspaceName,
        entry.BoardId,
        entry.BoardName,
        ParseDetails(entry.Details));

    private static Dictionary<string, string> ParseDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>A page of audit entries (newest first) plus the filtered total.</summary>
public sealed record AuditEntryPageDto(IReadOnlyList<AuditEntryDto> Items, int Total);

/// <summary>Instance-wide audit log for instance administrators (the endpoint
/// enforces the AdminOnly policy). Paging is zero-based.</summary>
public sealed record ListAuditEntriesQuery(AuditLogFilter Filter, int Page = 0, int PageSize = 25) : IMessage;

/// <summary>The audit log of one workspace and its boards, for the people who
/// manage its members: the owner, workspace Admins and instance administrators.</summary>
public sealed record ListWorkspaceAuditEntriesQuery(
    Guid WorkspaceId, AuditLogFilter Filter, int Page = 0, int PageSize = 25) : IMessage;

public static class AuditLogQueryHandler
{
    public const int MaxPageSize = 100;

    public static async Task<AuditEntryPageDto> HandleAsync(
        ListAuditEntriesQuery query,
        IAuditLogReader reader,
        CancellationToken ct) =>
        await PageAsync(reader, query.Filter, query.Page, query.PageSize, ct);

    public static async Task<Result<AuditEntryPageDto>> HandleAsync(
        ListWorkspaceAuditEntriesQuery query,
        IAuditLogReader reader,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<AuditEntryPageDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Workspace? workspace = await workspaces.GetWithMembersAsync(new WorkspaceId(query.WorkspaceId), ct);
        if (workspace is null || workspace.IsDeleted)
        {
            return Result.Failure<AuditEntryPageDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!await WorkspaceAccess.CanManageMembersAsync(workspace, currentUser.Id, users, ct))
        {
            return Result.Failure<AuditEntryPageDto>(DomainError.Forbidden(
                "workspaces.not_manager", "Only the workspace owner or an admin can read its audit log."));
        }

        // The route decides the workspace; a filter cannot widen it.
        AuditLogFilter scoped = query.Filter with { WorkspaceId = query.WorkspaceId };
        return Result.Success(await PageAsync(reader, scoped, query.Page, query.PageSize, ct));
    }

    private static async Task<AuditEntryPageDto> PageAsync(
        IAuditLogReader reader, AuditLogFilter filter, int page, int pageSize, CancellationToken ct)
    {
        int size = Math.Clamp(pageSize, 1, MaxPageSize);
        int skip = Math.Clamp(page, 0, 100_000) * size;
        (IReadOnlyList<AuditEntry> items, int total) = await reader.ListAsync(filter, skip, size, ct);
        return new AuditEntryPageDto(items.Select(AuditEntryDto.From).ToList(), total);
    }
}
