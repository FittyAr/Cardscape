using System.Security.Claims;
using Cardscape.Api.Logging;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Hooks the JwtBearer pipeline so that an
/// authenticated token whose <c>jti</c> has been
/// recorded in <see cref="IRevokedTokenRepository"/>
/// is rejected with HTTP 401. The hot path is
/// <c>IsRevokedAsync</c>, which is a single-row seek
/// against the unique index on <c>Jti</c>.
/// <para>
/// The handler is wired via
/// <c>JwtBearerEvents.OnTokenValidated</c> in
/// <c>AddApiAuthentication</c>. The DbContext lookup
/// is a fresh scope (the EF Core repositories are
/// scoped; the validator lives in a singleton
/// pipeline) so the validation query does not share
/// state with the request that follows.
/// </para>
/// <para>
/// The same lookup scope also rejects tokens whose user has
/// since been deactivated, soft-deleted or anonymised by an
/// administrator, so locking a user out from the Users page
/// takes effect on their next request instead of at token
/// expiry.
/// </para>
/// </summary>
public sealed class JwtRevocationValidator(
    IServiceScopeFactory scopeFactory,
    ILogger<JwtRevocationValidator> logger)
{
    public async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        // The /api/auth/revoke endpoint must remain
        // reachable with an already-revoked token so a
        // client can re-record the revocation
        // (idempotency) or record it after the validator
        // started rejecting on a different request. The
        // path is hard-coded because the endpoint is the
        // only one in the system that ever needs to be
        // reachable post-revocation; every other endpoint
        // honors the revoked-token check.
        string path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (path.Equals("/api/auth/revoke", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // .NET 8+ JwtBearer uses JsonWebToken by default
        // (not the legacy JwtSecurityToken). Pulling the
        // jti off the validated principal's claims keeps
        // the validator agnostic of which SecurityToken
        // subtype the handler emits.
        string? jti = context.Principal?.FindFirst("jti")?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IRevokedTokenRepository repository =
                scope.ServiceProvider.GetRequiredService<IRevokedTokenRepository>();
            bool isRevoked = await repository.IsRevokedAsync(jti, context.HttpContext.RequestAborted);
            if (isRevoked)
            {
                logger.RevokedJwtRejected(jti);
                context.Fail("The access token has been revoked.");
                return;
            }

            // Only user tokens carry a user id the users table
            // knows; any other subject is left to its own checks.
            string? rawUserId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(rawUserId, out Guid userId))
            {
                IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                User? user = await users.GetByIdAsync(new UserId(userId), context.HttpContext.RequestAborted);
                if (user is not null && (!user.IsActive || user.IsDeleted || user.IsAnonymised))
                {
                    logger.LockedOutUserJwtRejected(userId);
                    context.Fail("The account has been deactivated.");
                }
            }
        }
        catch (Exception ex)
        {
            // A failure to look up the revocation table must
            // never silently let a revoked token through. The
            // safe answer is "reject" (fail-closed). The
            // operator dashboard surfaces repeated look-up
            // failures so the deployer can intervene.
            logger.JwtRevocationLookupFailed(ex, jti);
            context.Fail("Could not verify token revocation status.");
        }
    }
}
