using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Users.Commands;
using Cardscape.Application.Users.Queries;
using Cardscape.Domain.Common;
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
///   <item><c>GET /api/admin/users</c> — the instance user
///         directory (search, status filter, paging).</item>
///   <item><c>POST /api/admin/users/{id}/deactivate</c> and
///         <c>POST /api/admin/users/{id}/reactivate</c> — switch
///         sign-in off / on without deleting.</item>
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

        group.MapGet("/", async (
            IMessageBus bus,
            CancellationToken ct,
            string? search = null,
            UserStatusFilter status = UserStatusFilter.All,
            int page = 0,
            int pageSize = 25) =>
        {
            AdminUserPageDto result = await bus.InvokeAsync<AdminUserPageDto>(
                new ListUsersForAdminQuery(search, status, page, pageSize), ct);
            return Results.Ok(result);
        }).Produces<AdminUserPageDto>(StatusCodes.Status200OK);

        group.MapPost("/{userId:guid}/deactivate", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserActiveCommand(userId, false), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/reactivate", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserActiveCommand(userId, true), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapGet("/{userId:guid}/export", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            UserDataExportDto? bundle = await bus.InvokeAsync<UserDataExportDto?>(
                new GetUserDataExportQuery(userId), ct);
            return bundle is null
                ? ApiProblemResults.NotFound("users.not_found", "The requested user was not found.")
                : Results.Ok(bundle);
        }).Produces<UserDataExportDto>(StatusCodes.Status200OK);

        group.MapDelete("/{userId:guid}", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SoftDeleteUserCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/restore", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new RestoreUserCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/anonymise", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new AnonymiseUserCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/restrict", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserRestrictedCommand(userId, true), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/unrestrict", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserRestrictedCommand(userId, false), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/admin", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserAdminCommand(userId, true), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{userId:guid}/unadmin", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(
                new SetUserAdminCommand(userId, false), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("/", async Task<IResult> (
            CreateUserRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            Result<AdminAccountResult> result = await bus.InvokeAsync<Result<AdminAccountResult>>(
                new CreateUserByAdminCommand(request.Email, request.DisplayName, request.IsAdmin, request.Language), ct);
            return result.IsSuccess
                ? Results.Created($"/api/admin/users/{result.Value.UserId}", result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces<AdminAccountResult>(StatusCodes.Status201Created);

        group.MapPost("/{userId:guid}/reset-password", async Task<IResult> (
            Guid userId, ResetPasswordByAdminRequest? request, IMessageBus bus, CancellationToken ct) =>
        {
            Result<AdminAccountResult> result = await bus.InvokeAsync<Result<AdminAccountResult>>(
                new ResetUserPasswordByAdminCommand(userId, request?.Language), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        }).Produces<AdminAccountResult>(StatusCodes.Status200OK);

        // The administrator confirmed the address by other means.
        group.MapPost("/{userId:guid}/verify-email", async Task<IResult> (
            Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            Result result = await bus.InvokeAsync<Result>(new MarkEmailVerifiedByAdminCommand(userId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        return app;
    }
}

public sealed record CreateUserRequest(string Email, string DisplayName, bool IsAdmin = false, string? Language = null);

public sealed record ResetPasswordByAdminRequest(string? Language = null);
