using Cardscape.Application.Authentication.Commands;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

        group.MapPost("/promote-self-admin", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new PromoteSelfToAdminCommand(), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
        });

        return app;
    }
}
