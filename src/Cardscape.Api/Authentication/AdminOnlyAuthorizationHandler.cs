using System.Security.Claims;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Authorization;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Authorization requirement that gates <c>/api/admin/*</c>
/// endpoints. A request is authorised when the
/// authenticated principal resolves to a registered
/// user whose <c>IsAdmin</c> flag is set in the database.
/// Non-authenticated requests are rejected; authenticated
/// non-admin users are rejected with 403 (the policy
/// scheme returns 403 via the standard forbidden path).
/// </summary>
public sealed class AdminOnlyRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Handler that resolves the <c>UserId</c> claim to a
/// <see cref="User"/> aggregate and consults the
/// <c>IsAdmin</c> flag. The lookup is per-request but
/// the EF Core change tracker caches the entity, so
/// back-to-back admin requests for the same user do not
/// re-query the database.
///
/// For the "authenticated but not admin" case the
/// handler simply does not call <c>context.Succeed</c>
/// — ASP.NET Core's default behaviour turns an
/// unsuccessful requirement into HTTP 403 via the
/// forbidden handler. The "no user id claim" case is
/// also a fail (the auth pipeline authenticated the user
/// without a name identifier, which is a deployment-skew
/// bug; a future v1.3.0 PR will log and surface a
/// structured 500 instead of silently 403).
/// </summary>
public sealed class AdminOnlyAuthorizationHandler(
    IUserRepository users,
    ILogger<AdminOnlyAuthorizationHandler> logger) : AuthorizationHandler<AdminOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOnlyRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        string? rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out Guid userIdGuid))
        {
            logger.LogWarning(
                "AdminOnly requirement evaluated for an authenticated principal without a parseable NameIdentifier claim. Returning failed (403).");
            return;
        }

        User? user = await users.GetByIdAsync(new UserId(userIdGuid));
        if (user is null)
        {
            logger.LogWarning(
                "AdminOnly requirement evaluated for user id {UserId} that has no row in the users table. Returning failed (403).",
                userIdGuid);
            return;
        }

        if (user.IsAdmin)
        {
            context.Succeed(requirement);
        }
    }
}
