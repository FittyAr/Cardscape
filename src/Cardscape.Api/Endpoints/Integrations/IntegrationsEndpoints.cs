using Cardscape.Application.Integrations.GitHub.Commands;
using Cardscape.Application.Integrations.GitHub.DTOs;
using Cardscape.Application.Integrations.GoogleDrive.Commands;
using Cardscape.Application.Integrations.GoogleDrive.DTOs;
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
/// REST endpoints for the Google Drive, GitHub, and inbound
/// email integrations. Slack has its own endpoint group under
/// <c>/api/workspaces/{id}/integrations/slack</c> because the
/// channel-management surface is workspace-scoped.
/// </summary>
public static class IntegrationsEndpoints
{
    public static IEndpointRouteBuilder MapGoogleDriveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/integrations/google")
            .RequireAuthorization()
            .WithTags("Integrations.GoogleDrive");

        group.MapGet("/connect", async ([FromQuery] Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<string>>(
                new GetGoogleDrivePickerUrlQuery(workspaceId), ct);
            return result.IsSuccess
                ? Results.Ok(new { pickerUrl = result.Value })
                : MapError(result.Error);
        });

        group.MapPost("/connect", async ([FromBody] ConnectGoogleDriveRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<GoogleDriveConnectionDto>>(
                new ConnectGoogleDriveCommand(
                    body.WorkspaceId, body.GoogleEmail, body.EncryptedRefreshToken),
                ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/attach", async ([FromBody] AttachGoogleDriveRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<Guid>>(
                new AttachGoogleDriveFileCommand(body.CardId, body.FileId, body.FileName), ct);
            return result.IsSuccess
                ? Results.Created($"/api/cards/{body.CardId}/attachments/{result.Value}", new { id = result.Value })
                : MapError(result.Error);
        });

        return app;
    }

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

        group.MapGet("/pulls", async ([FromQuery] string repoFullName, [FromQuery] string? state, IMessageBus bus, CancellationToken ct) =>
        {
            Guid boardId = Guid.Empty; // The board-id is in the JWT principal's claims; the MCP tool fetches it before calling here.
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

        // Public webhook surface — no authorization, but the
        // inbound-email providers sign the request (the
        // implementation verifies the signature when
        // configured). The handler resolves the address to a
        // workspace + list and dispatches a CreateCardCommand
        // through Wolverine.
        group.MapPost("/inbound", async (HttpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            using StreamReader reader = new(request.Body);
            string body = await reader.ReadToEndAsync(ct);

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            string provider = (request.Query["provider"].ToString()
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

    public sealed record ConnectGoogleDriveRequest(
        Guid WorkspaceId, string GoogleEmail, string EncryptedRefreshToken);
    public sealed record AttachGoogleDriveRequest(Guid CardId, string FileId, string? FileName);
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
