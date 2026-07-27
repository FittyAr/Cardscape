using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Workspaces.Queries;

/// <summary>
/// "Inbox" view: lists every pending invitation addressed to the
/// currently-authenticated user. Used by the top-bar bell and the
/// /invitations page.
/// </summary>
public sealed record ListPendingInvitationsForUserQuery() : IMessage;

public static class ListPendingInvitationsForUserQueryHandler
{
    public static async Task<Result<IReadOnlyList<WorkspaceInvitationDto>>> Handle(
        ListPendingInvitationsForUserQuery query,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null || string.IsNullOrWhiteSpace(currentUser.Email))
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var pending = await repository.ListPendingForEmailAsync(
            currentUser.Email, cancellationToken);
        var now = clock.UtcNow;
        var dtos = new List<WorkspaceInvitationDto>(pending.Count);
        foreach (var inv in pending)
        {
            // Defensive: filter terminal rows (repository may have
            // returned them if a stale ExpiresAt slipped in).
            if (!inv.IsActive(now))
            {
                continue;
            }

            var workspace = await workspaces.GetByIdAsync(inv.WorkspaceId, cancellationToken);
            dtos.Add(new WorkspaceInvitationDto(
                inv.Id.Value,
                inv.WorkspaceId.Value,
                workspace?.Name.Value ?? "(deleted workspace)",
                inv.Email,
                inv.Role,
                inv.InvitedBy,
                inv.InvitedAt,
                inv.ExpiresAt,
                inv.TokenPrefix));
        }

        return Result.Success<IReadOnlyList<WorkspaceInvitationDto>>(dtos);
    }
}

/// <summary>
/// Owner-only: lists every invitation a workspace has issued,
/// including accepted/revoked rows when <paramref name="IncludeTerminal"/>
/// is true. Used by the members page.
/// </summary>
public sealed record ListWorkspaceInvitationsQuery(Guid WorkspaceId, bool IncludeTerminal = false)
    : IMessage;

public static class ListWorkspaceInvitationsQueryHandler
{
    public static async Task<Result<IReadOnlyList<WorkspaceInvitationDto>>> Handle(
        ListWorkspaceInvitationsQuery query,
        IWorkspaceInvitationRepository repository,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            new WorkspaceId(query.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (workspace.OwnerId != currentUser.Id.Value)
        {
            return Result.Failure<IReadOnlyList<WorkspaceInvitationDto>>(DomainError.Forbidden(
                "workspaces.not_owner", "Only the workspace owner can list invitations."));
        }

        var rows = await repository.ListForWorkspaceAsync(
            query.WorkspaceId, query.IncludeTerminal, cancellationToken);
        var workspaceName = workspace.Name.Value;
        var dtos = rows
            .Select(inv => new WorkspaceInvitationDto(
                inv.Id.Value,
                inv.WorkspaceId.Value,
                workspaceName,
                inv.Email,
                inv.Role,
                inv.InvitedBy,
                inv.InvitedAt,
                inv.ExpiresAt,
                inv.TokenPrefix))
            .ToList();

        return Result.Success<IReadOnlyList<WorkspaceInvitationDto>>(dtos);
    }
}

/// <summary>Public projection of a workspace invitation.</summary>
public sealed record WorkspaceInvitationDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string Email,
    WorkspaceRole Role,
    Guid InvitedBy,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    string TokenPrefix);
