using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Users.Commands;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Users;

/// <summary>
/// Self-service endpoints for the authenticated caller. The
/// surface is intentionally tiny; everything that affects
/// another user stays under <c>/api/admin/users</c>.
/// </summary>
public static class UserSelfEndpoints
{
    public static IEndpointRouteBuilder MapUserSelfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users.Self");

        // BETA-8-API-#5 — see test-results/r8/r8-report.md.
        // The DSR (Art. 17) right of erasure used to be admin-only:
        // the only way a user could delete their own account was
        // to ask an admin to call DELETE /api/admin/users/{id}.
        // For a self-hostable product with a single owner that's
        // a blocker on a basic GDPR cleanup. This endpoint takes
        // the caller's id from the JWT (never from the URL) and
        // runs the same SoftDeleteUserCommand the admin path uses,
        // so the rest of the lifecycle (30-day grace period, the
        // retention sweeper, the PII clear) is identical.
        group.MapDelete("/me", async Task<IResult> (
            ICurrentUser currentUser,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }
            Result result = await bus.InvokeAsync<Result>(
                new SoftDeleteUserCommand(currentUser.Id.Value), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        }).RequireAuthorization();

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
