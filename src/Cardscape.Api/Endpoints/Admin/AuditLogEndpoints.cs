using Cardscape.Api.Filters;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Audit;
using Cardscape.Domain.Common;
using Wolverine;
using AdminOnlyPolicy = Cardscape.Api.Extensions.AdminOnlyPolicy;

namespace Cardscape.Api.Endpoints.Admin;

/// <summary>
/// The administration audit log (who invited, changed roles, deactivated
/// someone):
/// <list type="bullet">
///   <item><c>GET /api/admin/audit</c> — every entry, instance admins only.</item>
///   <item><c>GET /api/workspaces/{id}/audit</c> — one workspace and its
///         boards, for its owner, workspace Admins and instance admins.</item>
/// </list>
/// Both take <c>from</c>/<c>to</c> (ISO dates, <c>to</c> exclusive),
/// <c>action</c> (code prefix such as <c>user.</c>), <c>actorId</c>,
/// <c>targetId</c> (a user), <c>search</c> (names) and zero-based
/// <c>page</c>/<c>pageSize</c>; the admin route also takes <c>workspaceId</c>.
/// </summary>
public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/audit", async (
            IMessageBus bus,
            CancellationToken ct,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            string? action = null,
            Guid? actorId = null,
            Guid? targetId = null,
            Guid? workspaceId = null,
            string? search = null,
            int page = 0,
            int pageSize = 25) =>
        {
            AuditEntryPageDto result = await bus.InvokeAsync<AuditEntryPageDto>(
                new ListAuditEntriesQuery(
                    new AuditLogFilter(from, to, action, actorId, targetId, workspaceId, search), page, pageSize),
                ct);
            return Results.Ok(result);
        })
            .RequireAuthorization(AdminOnlyPolicy.Name)
            .WithTags("Admin.Audit")
            .Produces<AuditEntryPageDto>(StatusCodes.Status200OK);

        app.MapGet("/api/workspaces/{workspaceId:guid}/audit", async (
            Guid workspaceId,
            IMessageBus bus,
            CancellationToken ct,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            string? action = null,
            Guid? actorId = null,
            Guid? targetId = null,
            string? search = null,
            int page = 0,
            int pageSize = 25) =>
        {
            Result<AuditEntryPageDto> result = await bus.InvokeAsync<Result<AuditEntryPageDto>>(
                new ListWorkspaceAuditEntriesQuery(
                    workspaceId, new AuditLogFilter(from, to, action, actorId, targetId, null, search), page, pageSize),
                ct);
            return result.ToOk();
        })
            .RequireAuthorization()
            .RequireRegionGuard()
            .WithTags("Workspaces")
            .Produces<AuditEntryPageDto>(StatusCodes.Status200OK);

        return app;
    }
}
