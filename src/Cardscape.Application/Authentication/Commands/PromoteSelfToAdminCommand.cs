using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Dev-only application-layer command that promotes the
/// calling user to system-admin. Used by the
/// <c>POST /api/dev/promote-self-admin</c> endpoint,
/// which is registered only when the host environment is
/// Development. The endpoint is the test bootstrap for
/// the AdminOnly policy; production deploys do not wire
/// it (the IsAdmin flag is set by a seed migration or
/// another existing-admin grant).
/// </summary>
public sealed record PromoteSelfToAdminCommand;

/// <summary>
/// Output of the promote command. BETA-9-#2 — see
/// test-results/r9/r9-report.md. Carries the user id and
/// a freshly-issued access token so the caller can hit
/// <c>/api/admin/*</c> without a re-login round-trip.
/// </summary>
public sealed record PromoteSelfToAdminResult(Guid UserId, string AccessToken);

public static class PromoteSelfToAdminCommandHandler
{
    public static async Task<Result<PromoteSelfToAdminResult>> Handle(
        PromoteSelfToAdminCommand command,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        ITokenService tokens,
        CancellationToken cancellation)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<PromoteSelfToAdminResult>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        User? user = await users.GetByIdAsync(currentUser.Id, cancellation);
        if (user is null)
        {
            return Result.Failure<PromoteSelfToAdminResult>(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (!user.IsAdmin)
        {
            user.SetAdmin(true, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellation);
        }

        // Re-issue the access token so the cached `is_admin`
        // claim is in sync with the database row. The previous
        // implementation left the caller with their old token,
        // and the AdminOnly handler trusts the cached claim by
        // default — so the promote "succeeded" but the admin
        // surface still returned 403 until the next login.
        string accessToken = tokens.IssueAccessToken(user, roles: new[] { "admin" });
        return Result.Success(new PromoteSelfToAdminResult(user.Id.Value, accessToken));
    }
}
