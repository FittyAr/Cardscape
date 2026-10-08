using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Setup.Commands;
using Cardscape.Application.Setup.DTOs;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Api.Endpoints.Setup;

public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/setup").WithTags("Setup");

        group.MapGet("/status", async (
            IUserRepository users,
            IConfiguration config,
            CancellationToken ct) =>
        {
            bool hasUsers = await users.AnyAsync(ct);
            string provider = config["Database:Provider"] ?? "Sqlite";

            return Results.Ok(new SetupStatusResponse(
                IsInitialized: hasUsers,
                DatabaseProvider: provider));
        })
        .AllowAnonymous()
        .Produces<SetupStatusResponse>();

        group.MapPost("/initialize", async (
            InitializeSystemCommand command,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            Result<AuthResponse> result = await bus.InvokeAsync<Result<AuthResponse>>(command, ct);

            return result.IsSuccess
                ? Results.Created("/api/auth/me", result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        })
        .AllowAnonymous()
        .Produces<AuthResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
