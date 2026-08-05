using Cardscape.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
/// </summary>
public sealed class JwtRevocationValidator(
    IServiceScopeFactory scopeFactory,
    ILogger<JwtRevocationValidator> logger)
{
    public async Task OnTokenValidated(TokenValidatedContext context)
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
            using IServiceScope scope = scopeFactory.CreateScope();
            IRevokedTokenRepository repository =
                scope.ServiceProvider.GetRequiredService<IRevokedTokenRepository>();
            bool isRevoked = await repository.IsRevokedAsync(jti, context.HttpContext.RequestAborted);
            if (isRevoked)
            {
                logger.LogInformation(
                    "Rejecting revoked JWT (jti={Jti})",
                    jti);
                context.Fail("The access token has been revoked.");
            }
        }
        catch (Exception ex)
        {
            // A failure to look up the revocation table must
            // never silently let a revoked token through. The
            // safe answer is "reject" (fail-closed). The
            // operator dashboard surfaces repeated look-up
            // failures so the deployer can intervene.
            logger.LogError(
                ex, "Failed to look up revocation for jti={Jti}; failing closed.", jti);
            context.Fail("Could not verify token revocation status.");
        }
    }
}
