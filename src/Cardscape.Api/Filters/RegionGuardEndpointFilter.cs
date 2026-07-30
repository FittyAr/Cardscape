using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Workspaces;
using Microsoft.AspNetCore.Http;

namespace Cardscape.Api.Filters;

/// <summary>
/// Endpoint filter that enforces the data-residency policy at the
/// HTTP boundary. The data-residency feature (§4.5) ships a
/// <see cref="Workspace.GuardRegion(Region)"/> method on the
/// <see cref="Workspace"/> aggregate, an
/// <see cref="IDeploymentRegion"/> abstraction, and a migration,
/// but the guard was never wired into the request pipeline:
/// <c>GuardRegion</c> was never called and <c>IDeploymentRegion</c>
/// was registered but never resolved. This filter closes that gap.
/// <para>
/// For any request that carries a <c>workspaceId</c> route value
/// the filter loads the workspace, asks the deployment what
/// region it accepts, and runs <c>workspace.GuardRegion(...)</c>.
/// On mismatch it short-circuits the pipeline with a 422 and a
/// structured error body of the same shape the other workspace
/// endpoints emit (<c>{ error: { code, message } }</c>). On a
/// miss (no <c>workspaceId</c> route value, or the workspace
/// can't be loaded because it doesn't exist) the filter is a
/// no-op and the inner handler runs as normal — not-found
/// semantics stay with the inner handler.
/// </para>
/// <para>
/// The filter is registered per-group / per-endpoint via the
/// <see cref="EndpointConventionBuilderExtensions.RequireRegionGuard"/>
/// helper. Endpoints that don't take a <c>workspaceId</c> route
/// value (workspace creation, health checks, the Blazor fallback,
/// etc.) are not decorated with the helper and therefore don't
/// pay the cost of the database round trip.
/// </para>
/// </summary>
public sealed class RegionGuardEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;

        if (!TryGetWorkspaceId(http, out Guid workspaceId))
        {
            // No route-level workspace id — not a workspace-scoped
            // endpoint. The filter is a no-op so the inner handler
            // (e.g. workspace creation) runs as normal.
            return await next(context);
        }

        IServiceProvider services = http.RequestServices;
        IWorkspaceRepository workspaces = services.GetRequiredService<IWorkspaceRepository>();
        IDeploymentRegion deployment = services.GetRequiredService<IDeploymentRegion>();

        // Resolve the aggregate and let the domain method be the
        // single source of truth for the policy. If the workspace
        // doesn't exist we don't try to translate the failure into
        // a 404 here — the inner handler will do that with the
        // right error code and message.
        Workspace? workspace = await workspaces.GetByIdAsync(
            new WorkspaceId(workspaceId), http.RequestAborted);

        if (workspace is null)
        {
            return await next(context);
        }

        var guard = workspace.GuardRegion(deployment.Region);
        if (guard.IsFailure)
        {
            return Results.UnprocessableEntity(new
            {
                error = new
                {
                    code = guard.Error.Code,
                    message = guard.Error.Message
                }
            });
        }

        return await next(context);
    }

    private static bool TryGetWorkspaceId(HttpContext http, out Guid workspaceId)
    {
        workspaceId = default;

        if (!http.Request.RouteValues.TryGetValue("workspaceId", out object? raw)
            || raw is null)
        {
            return false;
        }

        // The route constraint is {workspaceId:guid}, so by the
        // time the endpoint executes the binding has already
        // parsed it. We re-parse defensively in case a caller
        // adds the filter to a route that doesn't pin the type.
        return Guid.TryParse(raw.ToString(), out workspaceId);
    }
}

/// <summary>Convenience extension that attaches
/// <see cref="RegionGuardEndpointFilter"/> to a route or group.
/// Use it on any <c>IEndpointConventionBuilder</c> whose endpoints
/// take a <c>workspaceId</c> route value.</summary>
public static class EndpointConventionBuilderExtensions
{
    public static TBuilder RequireRegionGuard<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        // Use the two-type-args AddEndpointFilter<TBuilder, TFilter>
        // overload so the call resolves to the generic extension
        // (which accepts any IEndpointConventionBuilder) and not the
        // RouteHandlerBuilder-only concrete overload.
        return builder.AddEndpointFilter<TBuilder, RegionGuardEndpointFilter>();
    }
}
