using System.Security.Cryptography;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Wolverine;

namespace Cardscape.Application.Webhooks;

public sealed record CreateWebhookEndpointCommand(
    Guid BoardId,
    string Url,
    string? Secret,
    IReadOnlyList<string> Events) : IMessage;

public static class CreateWebhookEndpointCommandHandler
{
    public static async Task<Result<WebhookEndpointIssuance>> Handle(
        CreateWebhookEndpointCommand command,
        IWebhookEndpointRepository endpoints,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (command.Events is null || command.Events.Count == 0)
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.Validation(
                "webhooks.events_required", "At least one event type is required."));
        }

        foreach (string e in command.Events)
        {
            if (!WebhookEventTypes.IsKnown(e))
            {
                return Result.Failure<WebhookEndpointIssuance>(DomainError.Validation(
                    "webhooks.event_unknown",
                    $"Unknown webhook event type '{e}'. Allowed: "
                    + string.Join(", ", WebhookEventTypes.All)));
            }
        }

        // BETA-9-#1 — see test-results/r9/r9-report.md.
        // The previous order checked the secret length BEFORE the
        // SSRF guard, so a request to `http://localhost:9999/evil`
        // with a one-character secret returned `webhooks.secret_too_short`
        // instead of the SSRF error. The SSRF guard is the security-
        // critical check and must be the first validation, so an
        // attacker probing the endpoint learns nothing about the
        // secret-length policy from a blocked SSRF attempt.
        // We do a defensive URL/SSRF validation here (the factory
        // runs the same checks again — they are idempotent and free).
        if (!Uri.TryCreate(command.Url, UriKind.Absolute, out Uri? parsedUrl)
            || (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.Validation(
                "webhooks.url_invalid", "Webhook URL must be an absolute http or https URL."));
        }
        if (WebhookUrlValidator.IsInternalHost(parsedUrl))
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.Validation(
                "webhooks.url_internal",
                "Webhook URL must not resolve to a private, loopback, link-local, or otherwise non-routable address."));
        }

        // BETA-6-#1 — see test-results/BETA-TEST-REPORT.md.
        // If the caller did not provide a secret, the server
        // generates one and returns it in the issuance payload.
        // This matches the Kanban / GitHub webhook flow: a
        // client never picks the shared secret, the server
        // hands it out once and the client has to copy it.
        string cleartext = string.IsNullOrWhiteSpace(command.Secret)
            ? GenerateWebhookSecret()
            : command.Secret;

        if (cleartext.Length < 8)
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.Validation(
                "webhooks.secret_too_short",
                "Webhook secret must be at least 8 characters."));
        }

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(command.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<WebhookEndpointIssuance>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        string protectedSecret = secretProtector.Protect(cleartext);

        // The factory validates URL/secret/events again; we
        // pre-filtered above to surface friendlier errors.
        var creation = WebhookEndpoint.Create(
            WebhookEndpointId.New(),
            new BoardId(command.BoardId),
            command.Url,
            protectedSecret,
            string.Join(",",
                command.Events
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e.Trim().ToLowerInvariant())
                    .Distinct()
                    .OrderBy(e => e, StringComparer.Ordinal)),
            clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<WebhookEndpointIssuance>(creation.Error);
        }

        await endpoints.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new WebhookEndpointIssuance(
            WebhookEndpointDto.FromEntity(creation.Value),
            cleartext));
    }

    private static string GenerateWebhookSecret()
    {
        // 32 random bytes → 64 hex chars. Way over the
        // 8-char minimum and unguessable.
        Span<byte> bytes = stackalloc byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}


