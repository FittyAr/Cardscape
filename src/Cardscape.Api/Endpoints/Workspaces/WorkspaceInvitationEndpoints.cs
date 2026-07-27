using Cardscape.Application.Workspaces.Commands;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Workspaces;

/// <summary>
/// REST surface for workspace invitations. Owner-only write paths
/// (<c>POST /api/workspaces/{id}/invitations</c>,
/// <c>DELETE /api/workspaces/{id}/invitations/{invId}</c>) sit on
/// the workspace group; the invitee-facing paths
/// (<c>GET /api/invitations/pending</c>,
/// <c>POST /api/invitations/accept</c>) sit on their own group
/// because the URL scope is the current user, not a workspace.
/// </summary>
public static class WorkspaceInvitationEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var wsGroup = app.MapGroup("/api/workspaces/{workspaceId:guid}/invitations")
            .RequireAuthorization()
            .WithTags("Workspace invitations");

        wsGroup.MapGet("/", async (
            Guid workspaceId,
            bool includeTerminal,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceInvitationDto>>>(
                new ListWorkspaceInvitationsQuery(workspaceId, includeTerminal), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        wsGroup.MapPost("/", async (
            Guid workspaceId,
            IssueWorkspaceInvitationBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceInvitationIssuanceDto>>(
                new IssueWorkspaceInvitationCommand(
                    workspaceId, body.Email, body.Role, body.Lifetime), ct);
            return result.IsSuccess
                ? Results.Created($"/api/workspaces/{workspaceId}/invitations/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        wsGroup.MapDelete("/{invitationId:guid}", async (
            Guid workspaceId,
            Guid invitationId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new RevokeWorkspaceInvitationCommand(invitationId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        var inboxGroup = app.MapGroup("/api/invitations")
            .RequireAuthorization()
            .WithTags("Workspace invitations");

        inboxGroup.MapGet("/pending", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceInvitationDto>>>(
                new ListPendingInvitationsForUserQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        inboxGroup.MapPost("/accept", async (
            AcceptWorkspaceInvitationBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(
                new AcceptWorkspaceInvitationCommand(body.Token), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public sealed record IssueWorkspaceInvitationBody(
        string Email,
        WorkspaceRole Role,
        TimeSpan? Lifetime = null);

    public sealed record AcceptWorkspaceInvitationBody(string Token);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
