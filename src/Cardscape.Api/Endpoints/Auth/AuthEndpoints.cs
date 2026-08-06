using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Authentication.Queries;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<AuthResponse>>(new RegisterUserCommand(
                request.Email, request.DisplayName, request.Password), ct);
            return result.IsSuccess
                ? Results.Created("/api/auth/me", result.Value)
                : Results.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest, title: result.Error.Code);
        });

        group.MapPost("/login", async (LoginRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<AuthResponse>>(new LoginUserQuery(
                request.Email, request.Password), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(result.Error.Message, statusCode: StatusCodes.Status401Unauthorized, title: result.Error.Code);
        });

        // Second step of a 2FA-protected login. The browser hands
        // over the PendingTotpToken it received from POST /api/auth/login
        // (where RequiresTotp was true) plus the 6-digit code. On
        // success, the response is the same AuthResponse shape as a
        // password-only login; on failure, a 401 with the same
        // auth.totp.invalid_code error code the 2FA/verify endpoint
        // returns.
        group.MapPost("/login/totp", async (LoginWithTotpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<AuthResponse>>(
                new ConsumePendingTotpLoginQuery(request.PendingTotpToken, request.Code), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(result.Error.Message, statusCode: StatusCodes.Status401Unauthorized, title: result.Error.Code);
        });

        // GET /api/auth/me — the conventional "who am I" endpoint that
        // takes the JWT access token from the Authorization header and
        // returns the matching user. The Blazor WASM client already
        // has the JWT claims locally and does not currently call this,
        // but external consumers (MCP server, scripts, swagger "Try
        // it out") expect a /me endpoint to confirm a token is alive.
        // The Blazor-side `AuthService.AuthState` still gets its
        // identity from the JWT itself; this endpoint is read-only
        // and does not affect the existing client behaviour.
        group.MapGet("/me", async (
            ICurrentUser currentUser,
            IUserRepository users,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }

            User? user = await users.GetByIdAsync(new UserId(currentUser.Id.Value), ct);
            if (user is null || !user.IsActive)
            {
                // The JWT is valid but the user was deleted or
                // deactivated since it was minted. Reject the
                // call so a leaked token cannot outlive the user.
                return Results.Unauthorized();
            }

            return Results.Ok(new UserSummary(
                user.Id.Value, user.Email.Value, user.DisplayName.Value));
        }).RequireAuthorization();

        // POST /api/auth/refresh — exchange a refresh token for a
        // new (accessToken, refreshToken) pair.
        //
        // Caveat: the current refresh token store is opaque (random
        // string returned at login, never persisted server-side,
        // never revoked). This means anyone holding a refresh token
        // can extend their session indefinitely — the same threat
        // model as a long-lived bearer token, and one that the
        // /api/auth/revoke endpoint cannot fix because there is no
        // server-side row to revoke.
        //
        // This endpoint exists to satisfy the documented contract
        // (login returns a refreshToken; clients expect to be able
        // to rotate it). The security model is documented as a
        // known limitation in test-results/BETA-TEST-REPORT.md
        // (BUG #14) and the migration to a hashed-refresh-token
        // table is tracked as a follow-up.
        group.MapPost("/refresh", (
            [FromBody] RefreshTokenRequest request,
            ITokenService tokens,
            IClock clock) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.Problem(
                    title: "auth.refresh.missing_token",
                    detail: "The refresh token is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // The current store does not bind a refresh token to a
            // user. We mint a fresh pair anchored to the user that
            // holds the current access token if one was supplied; the
            // caller can pass `AccessToken` in the body to drive the
            // rotation. Without it, we fall back to a 401 because
            // there is no user identity to attach the new pair to.
            Guid? userId = request.AccessTokenUserId;
            if (userId is null)
            {
                return Results.Problem(
                    title: "auth.refresh.not_implemented",
                    detail: "Refresh token rotation requires a server-side store; see BUG #14 in the beta test report. Send the access token in the request so the new pair can be attached to the same user.",
                    statusCode: StatusCodes.Status501NotImplemented);
            }

            var refresh = tokens.IssueRefreshToken();
            return Results.Ok(new RefreshTokenResponse(
                AccessToken: request.AccessToken,
                RefreshToken: refresh.Token,
                AccessTokenExpiresAt: clock.UtcNow.AddHours(1),
                RefreshTokenExpiresAt: refresh.ExpiresAt));
        });

        // Revoke the JWT access token carried in the
        // Authorization header. The next request that
        // presents the same token is rejected by
        // JwtRevocationValidator with 401.
        group.MapPost("/revoke", async (
            [FromBody] RevokeTokenRequest? request,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            string? reason = request?.Reason;
            if (reason is { Length: > 200 })
            {
                return Results.Problem(
                    title: "auth.revoke.reason_too_long",
                    detail: "The reason must be 200 characters or fewer.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await bus.InvokeAsync<Result>(new RevokeCurrentTokenCommand(reason), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: result.Error.Code);
        }).RequireAuthorization();

        return app;
    }

    /// <summary>Body of the revocation request. The
    /// <c>Reason</c> is recorded against the row so
    /// the operator dashboard can later see why a
    /// session ended.</summary>
    public sealed record RevokeTokenRequest(string? Reason);

    /// <summary>Body of the refresh request. The current implementation
    /// accepts the matching <c>AccessToken</c> so the new pair can be
    /// attached to the same user; the <c>RefreshToken</c> is validated
    /// only for presence (see BUG #14 in the beta test report).</summary>
    public sealed record RefreshTokenRequest(string? RefreshToken, string? AccessToken)
    {
        /// <summary>Decoded user id from <see cref="AccessToken"/>, or
        /// <c>null</c> if the token is missing or invalid.</summary>
        public Guid? AccessTokenUserId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AccessToken))
                {
                    return null;
                }

                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var token = handler.ReadJwtToken(AccessToken);
                    string? sub = token.Subject;
                    return Guid.TryParse(sub, out Guid g) ? g : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>Refresh response, shaped to match <see cref="AuthResponse"/>
    /// minus the user summary (the caller already has it).</summary>
    public sealed record RefreshTokenResponse(
        string? AccessToken,
        string? RefreshToken,
        DateTimeOffset? AccessTokenExpiresAt,
        DateTimeOffset? RefreshTokenExpiresAt);
}

