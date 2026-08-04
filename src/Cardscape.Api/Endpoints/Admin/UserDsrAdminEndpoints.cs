using Cardscape.Api.Extensions;
using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Users.Commands;
using Cardscape.Application.Users.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
using AdminOnlyPolicy = Cardscape.Api.Extensions.AdminOnlyPolicy;

namespace Cardscape.Api.Endpoints.Admin;

/// <summary>
/// Admin endpoints for the GDPR data-subject rights (DSR)
/// surface. The endpoints are gated by the
/// <see cref="AdminOnlyPolicy"/>; only
/// an admin can hit them on behalf of a data subject
/// (the controller is the data controller's
/// representative for self-hosted deploys).
///
/// <list type="bullet">
///   <item><c>GET /api/admin/users/{id}/export</c> — right of
///         access (Art. 15). Returns a JSON bundle with every
///         personal-data field associated with the user.</item>
///   <item><c>DELETE /api/admin/users/{id}</c> — right to
///         erasure (Art. 17, soft-delete + 30-day grace
///         period). The hard-delete + PII clear are the
///         retention sweeper's job.</item>
///   <item><c>POST /api/admin/users/{id}/restore</c> —
///         reverse a soft-delete within the grace period.</item>
///   <item><c>POST /api/admin/users/{id}/anonymise</c> —
///         force the final state (PII cleared) without
///         waiting for the grace period.</item>
///   <item><c>POST /api/admin/users/{id}/restrict</c> and
///         <c>POST /api/admin/users/{id}/unrestrict</c> —
///         right to restriction (Art. 18).</item>
///   <item><c>POST /api/admin/users/{id}/admin</c> and
///         <c>POST /api/admin/users/{id}/unadmin</c> —
///         grant / revoke the system-admin role.</item>
/// </list>
/// </summary>
public static class UserDsrAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserDsrAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users")
            .RequireAuthorization(AdminOnlyPolicy.Name)
            .WithTags("Admin.Dsr");

        group.MapGet("/{userId:guid}/export", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            UserDataExportDto? bundle = await bus.InvokeAsync<UserDataExportDto?>(
                new GetUserDataExportQuery(userId), ct);
            return bundle is null
                ? Results.NotFound()
                : Results.Ok(bundle);
        });

        group.MapDelete("/{userId:guid}", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SoftDeleteUserCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{userId:guid}/restore", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new RestoreUserCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{userId:guid}/anonymise", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new AnonymiseUserCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{userId:guid}/restrict", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserRestrictedCommand(userId, true), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{userId:guid}/unrestrict", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserRestrictedCommand(userId, false), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{userId:guid}/admin", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserAdminCommand(userId, true), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        group.MapPost("/{userId:guid}/unadmin", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserAdminCommand(userId, false), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        return app;
    }

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
