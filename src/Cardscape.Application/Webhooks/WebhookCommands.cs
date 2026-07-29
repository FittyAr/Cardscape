using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Wolverine;

namespace Cardscape.Application.Webhooks;

/// <summary>JSON shape of a webhook payload as posted to subscribers.</summary>
public sealed record WebhookPayload(
    string Event,
    Guid BoardId,
    DateTimeOffset OccurredAt,
    string DeliveryId,
    object Data);

/// <summary>Compact projection of an endpoint for the API/MCP/Web layers.</summary>
public sealed record WebhookEndpointDto(
    Guid Id,
    Guid BoardId,
    string Url,
    string SecretPrefix,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt)
{
    public static WebhookEndpointDto FromEntity(WebhookEndpoint e) => new(
        e.Id.Value,
        e.BoardId.Value,
        e.Url,
        SecretPrefix: e.SecretHash[..Math.Min(8, e.SecretHash.Length)],
        Events: e.Events
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList(),
        Active: e.Active,
        CreatedAt: e.CreatedAt);
}

/// <summary>Result of a create call: the new endpoint plus the
/// cleartext secret (returned once, never persisted).</summary>
public sealed record WebhookEndpointIssuance(WebhookEndpointDto Endpoint, string CleartextSecret);

/// <summary>Compact projection of a delivery row for the API/MCP/Web layers.</summary>
public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid EndpointId,
    string EventType,
    int Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt)
{
    public static WebhookDeliveryDto FromEntity(WebhookDelivery d) => new(
        d.Id.Value,
        d.EndpointId.Value,
        d.EventType,
        (int)d.Status,
        d.AttemptCount,
        d.LastAttemptAt,
        d.LastError,
        d.CreatedAt);
}

// ── Commands & queries ──────────────────────────────────────────

public sealed record CreateWebhookEndpointCommand(
    Guid BoardId,
    string Url,
    string Secret,
    IReadOnlyList<string> Events) : IMessage;

public static class CreateWebhookEndpointCommandHandler
{
    public static async Task<Result<WebhookEndpointIssuance>> Handle(
        CreateWebhookEndpointCommand command,
        IWebhookEndpointRepository endpoints,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
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

        if (string.IsNullOrWhiteSpace(command.Secret) || command.Secret.Length < 8)
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

        string secretHash = HashSecret(command.Secret);

        // The factory validates URL/secret/events again; we
        // pre-filtered above to surface friendlier errors.
        var creation = WebhookEndpoint.Create(
            WebhookEndpointId.New(),
            new BoardId(command.BoardId),
            command.Url,
            secretHash,
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
            command.Secret));
    }

    private static string HashSecret(string cleartext)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(cleartext), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record ListWebhookEndpointsQuery(Guid BoardId) : IMessage;

public static class ListWebhookEndpointsQueryHandler
{
    public static async Task<Result<IReadOnlyList<WebhookEndpointDto>>> Handle(
        ListWebhookEndpointsQuery query,
        IWebhookEndpointRepository endpoints,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WebhookEndpointDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(query.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<WebhookEndpointDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<WebhookEndpointDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<WebhookEndpoint> rows =
            await endpoints.ListForBoardAsync(new BoardId(query.BoardId), ct);
        return Result.Success<IReadOnlyList<WebhookEndpointDto>>(
            rows.Select(WebhookEndpointDto.FromEntity).ToList());
    }
}

public sealed record UpdateWebhookEndpointCommand(
    Guid EndpointId,
    string? Url,
    bool? Active) : IMessage;

public static class UpdateWebhookEndpointCommandHandler
{
    public static async Task<Result<WebhookEndpointDto>> Handle(
        UpdateWebhookEndpointCommand command,
        IWebhookEndpointRepository endpoints,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<WebhookEndpointDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        WebhookEndpoint? endpoint = await endpoints.GetByIdAsync(
            new WebhookEndpointId(command.EndpointId), ct);
        if (endpoint is null)
        {
            return Result.Failure<WebhookEndpointDto>(DomainError.NotFound(
                "webhooks.not_found", "Webhook endpoint was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(endpoint.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<WebhookEndpointDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        if (command.Url is not null)
        {
            Result change = endpoint.ChangeUrl(command.Url);
            if (change.IsFailure)
            {
                return Result.Failure<WebhookEndpointDto>(change.Error);
            }
        }

        if (command.Active is bool active)
        {
            if (active)
            {
                endpoint.Activate(clock.UtcNow);
            }
            else
            {
                endpoint.Deactivate(clock.UtcNow);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(WebhookEndpointDto.FromEntity(endpoint));
    }
}

public sealed record DeleteWebhookEndpointCommand(Guid EndpointId) : IMessage;

public static class DeleteWebhookEndpointCommandHandler
{
    public static async Task<Result> Handle(
        DeleteWebhookEndpointCommand command,
        IWebhookEndpointRepository endpoints,
        IBoardRepository boards,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        WebhookEndpoint? endpoint = await endpoints.GetByIdAsync(
            new WebhookEndpointId(command.EndpointId), ct);
        if (endpoint is null)
        {
            return Result.Failure(DomainError.NotFound(
                "webhooks.not_found", "Webhook endpoint was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(endpoint.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        endpoints.Remove(endpoint);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record ListWebhookDeliveriesQuery(
    Guid EndpointId,
    int? StatusFilter,
    int Skip,
    int Take) : IMessage;

public static class ListWebhookDeliveriesQueryHandler
{
    public static async Task<Result<IReadOnlyList<WebhookDeliveryDto>>> Handle(
        ListWebhookDeliveriesQuery query,
        IWebhookDeliveryRepository deliveries,
        IWebhookEndpointRepository endpoints,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<WebhookDeliveryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        WebhookEndpoint? endpoint = await endpoints.GetByIdAsync(
            new WebhookEndpointId(query.EndpointId), ct);
        if (endpoint is null)
        {
            return Result.Failure<IReadOnlyList<WebhookDeliveryDto>>(DomainError.NotFound(
                "webhooks.not_found", "Webhook endpoint was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(endpoint.BoardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<WebhookDeliveryDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        WebhookDeliveryStatus? statusFilter = query.StatusFilter is int s
            ? (WebhookDeliveryStatus)s
            : null;
        int skip = Math.Max(0, query.Skip);
        int take = Math.Clamp(query.Take, 1, 200);

        IReadOnlyList<WebhookDelivery> rows = await deliveries.ListForEndpointAsync(
            endpoint.Id, statusFilter, skip, take, ct);
        return Result.Success<IReadOnlyList<WebhookDeliveryDto>>(
            rows.Select(WebhookDeliveryDto.FromEntity).ToList());
    }
}

/// <summary>Command the dispatcher sends (or any application code
/// can send) to fan a single domain event out to every matching
/// active endpoint. The handler creates a delivery row and an
/// associated <c>BackgroundJob</c> for each endpoint, so retries
/// ride the same backoff infrastructure as everything else.</summary>
public sealed record EnqueueWebhookDeliveriesCommand(
    string EventType,
    Guid BoardId,
    object Data) : IMessage;

public static class EnqueueWebhookDeliveriesCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<Result<int>> Handle(
        EnqueueWebhookDeliveriesCommand command,
        IWebhookEndpointRepository endpoints,
        IWebhookDeliveryRepository deliveries,
        IBackgroundJobScheduler scheduler,
        IClock clock,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        if (!WebhookEventTypes.IsKnown(command.EventType))
        {
            return Result.Failure<int>(DomainError.Validation(
                "webhooks.event_unknown", $"Unknown event type '{command.EventType}'."));
        }

        IReadOnlyList<WebhookEndpoint> targets =
            await endpoints.ListActiveForEventAsync(command.EventType, ct);
        if (targets.Count == 0)
        {
            return Result.Success(0);
        }

        DateTimeOffset now = clock.UtcNow;
        int queued = 0;

        foreach (WebhookEndpoint endpoint in targets)
        {
            if (endpoint.BoardId.Value != command.BoardId)
            {
                // Don't fan an event on board X to endpoints
                // registered on board Y. The repository may
                // eventually return all matching endpoints across
                // boards once we add cross-board fan-out.
                continue;
            }

            // Build the delivery row first so we can include the
            // canonical id in the signed payload. The handler
            // re-reads the row by id at dispatch time.
            var deliveryId = WebhookDeliveryId.New();
            var payload = new WebhookPayload(
                Event: command.EventType,
                BoardId: command.BoardId,
                OccurredAt: now,
                DeliveryId: deliveryId.Value.ToString(),
                Data: command.Data);

            string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            var creation = WebhookDelivery.Create(
                endpoint.Id, command.EventType, payloadJson, now);
            if (creation.IsFailure)
            {
                continue;
            }

            await deliveries.AddAsync(creation.Value, ct);
            await unitOfWork.SaveChangesAsync(ct);

            var jobPayload = new WebhookDeliveryJobPayload(
                creation.Value.Id.Value,
                endpoint.Id.Value,
                command.EventType,
                payloadJson);
            await scheduler.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                jobPayload,
                scheduledFor: now,
                maxAttempts: 5,
                ct: ct);
            queued++;
        }

        return Result.Success(queued);
    }
}
