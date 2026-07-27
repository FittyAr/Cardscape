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
            var result = await bus.InvokeAsync<Result<AuthResponse>>(new LoginUserQuery(request.Email, request.Password), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(result.Error.Message, statusCode: StatusCodes.Status401Unauthorized, title: result.Error.Code);
        });

        return app;
    }
}
