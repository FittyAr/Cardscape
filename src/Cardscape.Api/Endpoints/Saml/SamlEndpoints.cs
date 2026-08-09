using Cardscape.Api.Filters;
using Cardscape.Application.Saml;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Saml;

/// <summary>
/// SAML 2.0 SSO endpoints. The four IdP-facing endpoints
/// under <c>/saml/{workspaceSlug}/{login,login-init,acs,metadata}</c>
/// are intercepted by <see cref="Cardscape.Api.Authentication.SamlAuthenticationHandler"/>
/// (an <c>IAuthenticationRequestHandler</c>) which drives
/// the Sustainsys.Saml2 protocol commands. The endpoint
/// routes below are kept as a documented fallback: if the
/// SAML scheme is not registered the endpoints return a
/// JSON 501 with the protocol endpoint paths so operators
/// can confirm the URL space is mounted.
///
/// The admin endpoints (configure / get / disable) sit
/// under the JWT-protected <c>/api/workspaces/{id}/saml</c>
/// group and use the standard <see cref="Wolverine"/> bus
/// for command dispatch.
/// </summary>
public static class SamlEndpoints
{
    public static IEndpointRouteBuilder MapSamlEndpoints(this IEndpointRouteBuilder app)
    {
        // IdP-facing (unauthenticated). The SamlAuthenticationHandler
        // intercepts these paths via IAuthenticationRequestHandler
        // before the endpoint runs. If the handler is not
        // registered (e.g. the Sustainsys package reference was
        // removed) the endpoint returns 501 so the failure is
        // surfaced clearly.
        var idp = app.MapGroup("/saml").WithTags("SAML");

        idp.MapGet("/{workspaceSlug}/login", (string workspaceSlug) =>
            Results.Problem(
                title: "saml.not_handled",
                detail: $"SAML handler is not registered. The /saml/{workspaceSlug}/login route exists but is not being processed.",
                statusCode: StatusCodes.Status501NotImplemented));

        idp.MapGet("/{workspaceSlug}/login-init", (string workspaceSlug) =>
            Results.Problem(
                title: "saml.not_handled",
                detail: $"SAML handler is not registered. The /saml/{workspaceSlug}/login-init route exists but is not being processed.",
                statusCode: StatusCodes.Status501NotImplemented));

        idp.MapPost("/{workspaceSlug}/acs", () =>
            Results.Problem(
                title: "saml.not_handled",
                detail: "SAML handler is not registered. The /saml/{slug}/acs route exists but is not being processed.",
                statusCode: StatusCodes.Status501NotImplemented));

        idp.MapGet("/{workspaceSlug}/metadata", (string workspaceSlug) =>
            Results.Problem(
                title: "saml.not_handled",
                detail: $"SAML handler is not registered. The /saml/{workspaceSlug}/metadata route exists but is not being processed.",
                statusCode: StatusCodes.Status501NotImplemented));

        // Admin (authenticated).
        var admin = app.MapGroup("/api/workspaces/{workspaceId:guid}/saml")
            .RequireAuthorization()
            .RequireRegionGuard()
            .WithTags("SAML.Admin");

        admin.MapGet("/", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<SamlConnectionDto?>>(
                new GetSamlConnectionQuery(workspaceId), ct);
            // BETA-A2-004 — see test-results/beta/00-FINAL-SUMMARY.md.
            // When the workspace has no SAML connection the
            // handler returns `SamlConnectionDto?` null. The
            // previous `Results.Ok(result.Value)` serialised a
            // 0-byte body and the WASM client's
            // `ReadFromJsonAsync` threw `JsonException`. Bounce
            // null to `Results.NoContent()` so the client sees
            // the canonical "not configured" response (the
            // existing `ApiClientBase.ReadAsync` handles a 204
            // and returns Ok(default) for nullable T).
            return result.IsSuccess
                ? result.Value is null
                    ? Results.NoContent()
                    : Results.Ok(result.Value)
                : MapError(result.Error);
        });

        admin.MapPost("/", async (
            Guid workspaceId,
            [FromBody] ConfigureSamlBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<SamlConnectionDto>>(
                new ConfigureSamlConnectionCommand(
                    workspaceId, body.Slug, body.DisplayName, body.IdpEntityId,
                    body.IdpMetadataUrl, body.IdpMetadataXml, body.SpEntityId),
                ct);
            return result.IsSuccess
                ? Results.Created($"/api/workspaces/{workspaceId}/saml", result.Value)
                : MapError(result.Error);
        });

        admin.MapDelete("/", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new DisableSamlConnectionCommand(workspaceId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record ConfigureSamlBody(
        string Slug,
        string DisplayName,
        string IdpEntityId,
        string IdpMetadataUrl,
        string? IdpMetadataXml,
        string SpEntityId);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
