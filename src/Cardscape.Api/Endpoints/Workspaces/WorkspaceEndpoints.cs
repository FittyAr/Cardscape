using Cardscape.Api.Filters;
using Cardscape.Application.Workspaces.Commands;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Workspaces;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        // The filter is a no-op for endpoints that don't carry a
        // workspaceId route value (the GET / and POST / workspace
        // creation endpoints) so it's safe to apply at the group
        // level; the other endpoints all do.
        var group = app.MapGroup("/api/workspaces")
            .RequireAuthorization()
            .RequireRegionGuard()
            .WithTags("Workspaces");

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceDto>>>(new ListWorkspacesForUserQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapGet("/{workspaceId:guid}", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(new GetWorkspaceQuery(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async ([FromBody] CreateWorkspaceRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(
                new CreateWorkspaceCommand(body.Name, body.Region), ct);
            return result.IsSuccess
                ? Results.Created($"/api/workspaces/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/{workspaceId:guid}/region", async (Guid workspaceId, [FromBody] SetWorkspaceRegionRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(
                new SetWorkspaceRegionCommand(workspaceId, body.Region), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // Owner-only: toggle the workspace's two-factor
        // authentication requirement. The endpoint is a plain
        // POST that accepts the new state in the body; the
        // aggregate method is idempotent, so re-POSTing the
        // current value is a no-op (no event, no UpdatedAt
        // bump).
        group.MapPost("/{workspaceId:guid}/security/require-2fa", async (
            Guid workspaceId,
            [FromBody] SetWorkspaceRequireTwoFactorRequest body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(
                new SetWorkspaceRequireTwoFactorCommand(workspaceId, body.Require), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{workspaceId:guid}/rename", async (Guid workspaceId, RenameWorkspaceRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(new RenameWorkspaceCommand(workspaceId, body.Name), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{workspaceId:guid}/archive", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(new ArchiveWorkspaceCommand(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BETA-A2-001 — see test-results/beta/00-FINAL-SUMMARY.md.
        // Restores a previously archived workspace. Pairs with the
        // /archive endpoint. Owner-only, same authz model.
        group.MapPost("/{workspaceId:guid}/unarchive", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(new UnarchiveWorkspaceCommand(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BETA-R2-A2-009 — see test-results/beta/round-2/reports/A2-workspaces.md.
        // The round-1 surface shipped only archive/unarchive; there
        // was no path to actually delete a workspace. This is the
        // soft-delete endpoint (the row is hidden from default
        // queries, kept in the table for audit) and completes the
        // workspace lifecycle.
        group.MapDelete("/{workspaceId:guid}", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteWorkspaceCommand(workspaceId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapGet("/{workspaceId:guid}/members", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceMemberDto>>>(new ListWorkspaceMembersQuery(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{workspaceId:guid}/members", async (Guid workspaceId, AddWorkspaceMemberRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(new AddWorkspaceMemberCommand(workspaceId, body.UserId, body.Role), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BETA-R2-A2-011 — see test-results/beta/round-2/reports/A2-workspaces.md.
        // The DTO `ChangeWorkspaceMemberRoleRequest` was already
        // declared but there was no command, no handler and no
        // endpoint wired up to it. Owner-only, mirrors the
        // RemoveWorkspaceMemberCommand authz model.
        group.MapPatch("/{workspaceId:guid}/members/{userId:guid}", async (
            Guid workspaceId,
            Guid userId,
            ChangeWorkspaceMemberRoleRequest body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(
                new ChangeWorkspaceMemberRoleCommand(workspaceId, userId, body.Role), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{workspaceId:guid}/members/{userId:guid}", async (Guid workspaceId, Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<WorkspaceDto>>(new RemoveWorkspaceMemberCommand(workspaceId, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        Cardscape.Domain.Common.ErrorType.Validation => Results.UnprocessableEntity(new { error.Code, error.Message }),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
