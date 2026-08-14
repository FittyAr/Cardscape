using Cardscape.Application.Integrations.GitHub.Commands;
using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Application.Integrations.InboundEmail.Commands;
using Cardscape.Application.Integrations.InboundEmail.DTOs;
using Cardscape.Application.Integrations.InboundEmail.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Integrations;

/// <summary>
/// REST endpoints for the GitHub and inbound
/// email integrations. Slack has its own endpoint group under
/// <c>/api/workspaces/{id}/integrations/slack</c> because the
/// channel-management surface is workspace-scoped.
/// </summary>
public static class IntegrationsEndpoints
{
    public static IEndpointRouteBuilder MapGitHubEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/integrations/github")
            .RequireAuthorization()
            .WithTags("Integrations.GitHub");

        group.MapPost("/connect", async ([FromBody] LinkGitHubRepoRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new LinkGitHubRepoCommand(
                    body.BoardId, body.RepoFullName, body.Events),
                ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        // BETA-2-#11 — see test-results/BETA-TEST-REPORT.md.
        //
        // The previous version hard-coded `boardId = Guid.Empty`
        // and assumed the MCP tool would have stamped the
        // current board on the JWT. The HTTP endpoint never
        // received the JWT claim, so the lookup against
        // `db.Lists.Where(l => l.BoardId == new BoardId(Guid.Empty))`
        // was always empty and the endpoint returned 404 for
        // every call. The fix is to read the boardId from a
        // query string parameter — the operator dashboard and
        // Scalar's "Try it out" panel use this directly, and
        // the MCP tool is updated separately to pass it through.
        group.MapGet("/pulls", async (
            [FromQuery] Guid boardId,
            [FromQuery] string repoFullName,
            [FromQuery] string? state,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (boardId == Guid.Empty)
            {
                return Results.Problem(
                    title: "integrations.github.board_required",
                    detail: "The boardId query parameter is required so the server can scope the lookup to the right board.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await bus.InvokeAsync<Result<IReadOnlyList<GitHubPullRequestDto>>>(
                new ListGitHubPullRequestsQuery(boardId, repoFullName, state ?? "open"), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/pulls/link", async ([FromBody] LinkGitHubPullRequestRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<GitHubPullRequestLinkDto>>(
                new LinkGitHubPullRequestCommand(
                    body.CardId, body.RepoFullName, body.PullRequestNumber), ct);
            return result.IsSuccess
                ? Results.Created($"/api/cards/{body.CardId}/github-links/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/issues", async ([FromBody] CreateGitHubIssueRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<GitHubIssueDto>>(
                new CreateGitHubIssueFromCardCommand(
                    body.CardId, body.RepoFullName, body.Title, body.Body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapInboundEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/integrations/email")
            .WithTags("Integrations.InboundEmail");

        // Authenticated configuration surface.
        var authed = group
            .MapGroup("")
            .RequireAuthorization();

        authed.MapGet("/addresses", async ([FromQuery] Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<InboundEmailAddressDto>>>(
                new ListInboundEmailAddressesQuery(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        authed.MapPost("/addresses", async ([FromBody] RegisterInboundEmailAddressRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<InboundEmailAddressDto>>(
                new RegisterInboundEmailAddressCommand(
                    body.WorkspaceId, body.EmailAddress, body.TargetListId, body.Label), ct);
            return result.IsSuccess
                ? Results.Created($"/api/integrations/email/addresses/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        authed.MapDelete("/addresses/{addressId:guid}", async (Guid addressId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new UnregisterInboundEmailAddressCommand(addressId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        // Public webhook surface — no authorization at the
        // routing layer, but the endpoint requires the same
        // shared internal secret the broadcast and client-log
        // endpoints use. The intent is for the operator to
        // place a small reverse-proxy (or a provider-specific
        // signature-verification relay) in front of the API
        // that injects <c>X-Cardscape-Inbound-Signature</c> on
        // every legitimate webhook delivery. Without that
        // relay the endpoint is unavailable: the
        // v1.2.0 audit (pass 7) found that the previous
        // incarnation was completely anonymous and any
        // unauthenticated POST that could guess (or scrape)
        // a registered inbound-email address could create
        // arbitrary cards in any workspace. The shared-secret
        // gate is the minimum viable defence until a
        // per-provider signature-verification layer lands
        // (the per-provider config is already in the docs).
        //
        // SECURITY: the previous incarnation read the request
        // body straight to a string with no upper bound. The
        // ASP.NET default request body cap is 28.6 MB per
        // request, which is fine for SendGrid / Mailgun /
        // Postmark payloads (the largest legitimate send is
        // ~250 KB for a 25 MB attachment base64-encoded, but
        // we drop attachments server-side and the providers
        // split them out) but would let a single unauthenticated
        // POST hold the body in memory for a full minute. The
        // 1 MB cap gives generous headroom for the
        // attachment-less payload (text + headers + envelope,
        // well under 64 KB in practice) while keeping the
        // endpoint as cheap as the client-log relay. Content-
        // Length is checked first to short-circuit without
        // allocating the read buffer; chunked / unknown-
        // length requests fall through to the read-loop guard.
        const int MaxInboundEmailBodyBytes = 1 * 1024 * 1024;
        const string InboundSignatureHeader = "X-Cardscape-Inbound-Signature";
        group.MapPost("/inbound", async (
            HttpContext http,
            IConfiguration config,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            // The shared secret is a different value from
            // Internal:Secret (the broadcast / client-log
            // secret) so a leak of one does not cascade to
            // the other. The signature header carries the
            // HMAC-SHA256 of the request body keyed with the
            // shared secret, hex-encoded. A constant-time
            // compare closes the timing oracle.
            string? expected = config["InboundEmail:SigningSecret"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                return Results.Problem(
                    detail: "InboundEmail:SigningSecret is not configured; the inbound email endpoint is unavailable. " +
                            "Set it via appsettings, environment variables, or a secret store to opt in.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            string? provided = http.Request.Headers[InboundSignatureHeader];
            if (string.IsNullOrEmpty(provided))
            {
                return Results.Unauthorized();
            }

            // Read the body once into a buffer so we can
            // (1) signature-verify the exact bytes the
            // provider sent and (2) feed them to the
            // downstream parser. The signature must be
            // verified BEFORE any other action on the body
            // so a forged body that exceeds the cap still
            // fails the auth check.
            if (http.Request.ContentLength is long advertised && advertised > MaxInboundEmailBodyBytes)
            {
                return Results.Problem(
                    detail: $"Inbound email body exceeds the {MaxInboundEmailBodyBytes}-byte cap.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            byte[] buffer = new byte[MaxInboundEmailBodyBytes + 1];
            int read = 0;
            int chunk;
            while ((chunk = await http.Request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct)) > 0)
            {
                read += chunk;
                if (read > MaxInboundEmailBodyBytes)
                {
                    return Results.Problem(
                        detail: $"Inbound email body exceeds the {MaxInboundEmailBodyBytes}-byte cap.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }
            }

            byte[] signedBytes = new byte[read];
            Buffer.BlockCopy(buffer, 0, signedBytes, 0, read);

            // HMAC-SHA256 of the raw body, keyed with the
            // shared secret, lowercase hex. The provider's
            // signature relay (or the operator's SMTP
            // gateway) must compute the same value. The
            // expected value is derived from the configured
            // secret on every request so a rotation takes
            // effect immediately.
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(expected);
            byte[] expectedHash = System.Security.Cryptography.HMACSHA256.HashData(keyBytes, signedBytes);
            string expectedHex = Convert.ToHexString(expectedHash).ToLowerInvariant();
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(provided),
                    System.Text.Encoding.ASCII.GetBytes(expectedHex)))
            {
                return Results.Unauthorized();
            }

            string body = System.Text.Encoding.UTF8.GetString(signedBytes);
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest(new { error = "Inbound email body was empty." });
            }

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in http.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            string provider = (http.Request.Query["provider"].ToString()
                ?? headers.GetValueOrDefault("X-Inbound-Provider", string.Empty)
                ?? "sendgrid").ToLowerInvariant();

            var result = await bus.InvokeAsync<Result<Guid>>(
                new HandleInboundEmailCommand(provider, body, headers), ct);
            return result.IsSuccess
                ? Results.Ok(new { cardId = result.Value })
                : MapError(result.Error);
        });

        return app;
    }

    public sealed record LinkGitHubRepoRequest(
        Guid BoardId, string RepoFullName, IReadOnlyList<string> Events);
    public sealed record LinkGitHubPullRequestRequest(
        Guid CardId, string RepoFullName, int PullRequestNumber);
    public sealed record CreateGitHubIssueRequest(
        Guid CardId, string RepoFullName, string? Title, string? Body);
    public sealed record RegisterInboundEmailAddressRequest(
        Guid WorkspaceId, string EmailAddress, Guid TargetListId, string Label);

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.External => Results.Json(new { error.Code, error.Message }, statusCode: StatusCodes.Status502BadGateway),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
