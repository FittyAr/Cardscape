using System.Security.Claims;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Authorization requirement that gates <c>/api/admin/*</c>
/// endpoints. The handler consults the
/// <see cref="AdminAuthorizationOptions.CacheAdminClaim"/>
/// configuration to decide whether to trust the
/// <c>is_admin</c> claim embedded in the JWT at mint time
/// (the fast path) or to read the live value from the
/// database on every check (the strict path). A token
/// issued before the v1.2.0 rollout (no <c>is_admin</c>
/// claim) always falls back to the users-table lookup, so
/// the migration is automatic and existing tokens keep
/// working until they expire.
/// </summary>
public sealed class AdminOnlyRequirement : IAuthorizationRequirement
{
}

public sealed class AdminOnlyAuthorizationHandler(
    IUserRepository users,
    IOptions<AdminAuthorizationOptions> options,
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

        // 1) Cached path — only consulted when the operator
        //    opted in via Cardscape:Api:AdminAuthorization:
        //    CacheAdminClaim = true (the default). When the
        //    claim is present the handler trusts it as the
        //    snapshot of the user's admin status at JWT
        //    mint time and never touches the database. When
        //    CacheAdminClaim is false, this branch is
        //    skipped entirely and every check hits the
        //    users table — that's the recommended posture
        //    for high-compliance deployments where admin
        //    revocation must take effect immediately.
        if (options.Value.CacheAdminClaim)
        {
            Claim? cached = context.User.FindFirst(IsAdminClaim);
            if (cached is not null)
            {
                if (string.Equals(cached.Value, "true", StringComparison.Ordinal))
                {
                    context.Succeed(requirement);
                }
                return;
            }
        }

        // 2) Live lookup. Always used when the cached path
        //    is disabled; used as a fallback for pre-v1.2.0
        //    tokens when the cached path is enabled but the
        //    claim is absent. A single-row seek by primary
        //    key; the user table is small and indexed.
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
