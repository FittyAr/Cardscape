using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Workspaces.Commands;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for workspace invitations. The owning user
/// can <c>workspaces_invite</c> and <c>workspaces_revoke_invitation</c>;
/// the invitee can <c>invitations_list_pending</c> and
/// <c>invitations_accept</c>. The cleartext token is returned
/// exactly once at issuance, so the model can pass it back to the
/// user (or feed it straight to <c>invitations_accept</c> in tests).
/// </summary>
[McpServerToolType]
public sealed class WorkspaceInvitationsTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "workspaces_invite")]
    public async Task<WorkspaceInvitationIssuanceDto> Invite(
        Guid workspaceId, string email, int role, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("workspaces_invite");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<WorkspaceInvitationIssuanceDto>>(
            new IssueWorkspaceInvitationCommand(
                workspaceId, email, (WorkspaceRole)role, Lifetime: null),
            ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "workspaces_list_invitations")]
    public async Task<IReadOnlyList<WorkspaceInvitationDto>> ListInvitations(
        Guid workspaceId, bool includeTerminal, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("workspaces_list_invitations");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceInvitationDto>>>(
            new ListWorkspaceInvitationsQuery(workspaceId, includeTerminal), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "workspaces_revoke_invitation")]
    public async Task<string> RevokeInvitation(Guid invitationId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("workspaces_revoke_invitation");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(
            new RevokeWorkspaceInvitationCommand(invitationId), ct);
        Ensure(result);
        return "revoked";
    }

    [McpServerTool(Name = "invitations_list_pending")]
    public async Task<IReadOnlyList<WorkspaceInvitationDto>> ListPendingInvitations(
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("invitations_list_pending");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceInvitationDto>>>(
            new ListPendingInvitationsForUserQuery(), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "invitations_accept")]
    public async Task<WorkspaceDto> AcceptInvitation(string token, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("invitations_accept");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<WorkspaceDto>>(
            new AcceptWorkspaceInvitationCommand(token), ct);
        return Ensure(result);
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT or API token in the Authorization header.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }
    }
}

