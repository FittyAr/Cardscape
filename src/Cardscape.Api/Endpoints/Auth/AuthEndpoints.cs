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
using Microsoft.Extensions.Hosting;
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

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            HttpContext http,
            IHostEnvironment environment,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            string? ip = http.Connection.RemoteIpAddress?.ToString();
            var result = await bus.InvokeAsync<Result<PasswordResetRequestResult>>(
                new RequestPasswordResetCommand(request.Email, ip, environment.IsDevelopment()), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest, title: result.Error.Code);
        });

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<bool>>(
                new ResetPasswordCommand(request.Token, request.NewPassword), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest, title: result.Error.Code);
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
        // but external consumers (MCP server, scripts, the Scalar
        // "Try it out" panel) expect a /me endpoint to confirm a
        // token is alive.
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

}

/// <summary>Body for <c>POST /api/auth/forgot-password</c>.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Body for <c>POST /api/auth/reset-password</c>.</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);
