using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Authentication.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

        return app;
    }
}
