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

public sealed record ListWebhookDeliveriesQuery(
    Guid BoardId,
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

        if (endpoint.BoardId.Value != query.BoardId)
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
