using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.Commands;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Dev;

/// <summary>
/// Dev-only endpoints that ship only in the
/// <c>Development</c> environment. The endpoints are
/// not registered in any other environment, so a
/// production deploy never exposes them. The intent
/// is to give the integration test suite a one-off
/// bootstrap for the <c>AdminOnly</c> policy: the test
/// fixture boots the host with
/// <c>ASPNETCORE_ENVIRONMENT=Development</c>, registers
/// a user, and promotes them to admin via this
/// endpoint to exercise the policy's success path.
/// </summary>
public static class DevOnlyEndpoints
{
    public static IEndpointRouteBuilder MapDevOnlyEndpoints(this IEndpointRouteBuilder app)
    {
        // MapDevOnly is called from Program.cs inside
        // an `if (app.Environment.IsDevelopment())` block,
        // so the routes are not registered in any other
        // environment.
        var group = app.MapGroup("/api/dev")
            .WithTags("Dev")
            .AllowAnonymous();

        // BETA-9-#2 — see test-results/r9/r9-report.md.
        // The previous implementation returned 204 No Content
        // after the promote, leaving the caller with the JWT
        // they had BEFORE the promote. The AdminOnly handler
        // trusts the cached `is_admin` claim by default, so
        // every admin/* endpoint kept returning 403 even though
        // the database row said the user was an admin. Re-login
        // would have fixed it, but the dev bootstrap is supposed
        // to be a one-shot. We now return a fresh access token
        // minted with the updated role, plus the user's id, so
        // the test fixture (or a human poking around the dev
        // build) can immediately hit the admin surface.
        group.MapPost("/promote-self-admin", async (
            IMessageBus bus,
            ITokenService tokens,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<PromoteSelfToAdminResult>>(
                new PromoteSelfToAdminCommand(), ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
            }

            return Results.Ok(new
            {
                userId = result.Value.UserId,
                accessToken = result.Value.AccessToken
            });
        });

        // BETA-A4 — dev-only bootstrap for the beta-test agent.
        // The agent landed in the test environment with the
        // beta-tester account already enrolled in 2FA but
        // without the recovery codes; the TOTP secret is
        // stored encrypted and the agent has no way to ask
        // the user. This endpoint soft-deletes the TOTP
        // credential for the named email so the next login
        // skips 2FA. Production deploys do not register the
        // endpoint (see MapDevOnly's IsDevelopment guard).
        group.MapPost("/disable-totp", async (
            [FromBody] DevDisableTotpRequest body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<DevDisableTotpResult>>(
                new DevDisableTotpCommand(body.Email), ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
            }

            return Results.Ok(new
            {
                userId = result.Value.UserId,
                hadCredential = result.Value.HadCredential
            });
        });

        return app;
    }
}

/// <summary>Body for <c>POST /api/dev/disable-totp</c>.</summary>
public sealed record DevDisableTotpRequest(string Email);
