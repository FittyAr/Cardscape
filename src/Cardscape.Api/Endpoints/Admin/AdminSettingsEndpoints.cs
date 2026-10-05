using Cardscape.Api.Extensions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Admin;

public static class AdminSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAdminSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/settings")
            .WithTags("AdminSettings")
            .RequireAuthorization(AdminOnlyPolicy.Name);

        group.MapGet("/", async (
            ISystemSettingsService settingsService,
            CancellationToken ct) =>
        {
            SystemSettingsDto settings = await settingsService.GetSettingsAsync(ct);
            return Results.Ok(settings);
        })
        .Produces<SystemSettingsDto>();

        group.MapPut("/", async (
            UpdateSystemSettingsRequest request,
            ISystemSettingsService settingsService,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            SystemSettingsDto updated = await settingsService.UpdateSettingsAsync(
                request, currentUser.Email, ct);
            return Results.Ok(updated);
        })
        .Produces<SystemSettingsDto>();

        return app;
    }
}
