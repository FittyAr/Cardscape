using System.Security.Claims;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Authorization;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Authorization requirement that gates <c>/api/admin/*</c>
/// endpoints. The handler reads the <c>is_admin</c> claim
/// that <c>JwtTokenService</c> mints at login time; no DB
/// lookup is needed. A token issued before the v1.2.0
/// rollout (no <c>is_admin</c> claim) falls back to the
/// users-table lookup so the migration is automatic and
/// existing tokens keep working until they expire.
/// </summary>
public sealed class AdminOnlyRequirement : IAuthorizationRequirement
{
}

public sealed class AdminOnlyAuthorizationHandler(
    IUserRepository users,
    ILogger<AdminOnlyAuthorizationHandler> logger) : AuthorizationHandler<AdminOnlyRequirement>
{
    private const string IsAdminClaim = "is_admin";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOnlyRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // 1) Cached path — read the claim the JWT mint flow
        //    embedded. The claim is "true" or "false" (the
        //    JwtTokenService writes it as a string so a
        //    missing claim reads as absent, not as false).
        Claim? cached = context.User.FindFirst(IsAdminClaim);
        if (cached is not null)
        {
            if (string.Equals(cached.Value, "true", StringComparison.Ordinal))
            {
                context.Succeed(requirement);
            }
            return;
        }

        // 2) Fallback for pre-v1.2.0 tokens that don't have
        //    the claim. Once the token expires (default
        //    60 minutes), the user re-authenticates and the
        //    claim lands in the new JWT.
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

/// <summary>
/// Dedicated policy name for the
/// <c>/api/admin/mcp-subscriptions</c> endpoint. Shares
/// the <see cref="AdminOnlyRequirement"/> + cached-claim
/// logic with the broader <c>AdminOnlyPolicy</c> but is
/// named distinctly so the
/// <c>McpSubscriptionsAdminEndpoints</c> can opt in to
/// dedicated telemetry + a future rate-limit policy
/// without affecting the rest of the admin surface.
/// </summary>
public static class McpSubscriptionsAdminPolicy
{
    public const string Name = "McpSubscriptionsAdmin";
}
