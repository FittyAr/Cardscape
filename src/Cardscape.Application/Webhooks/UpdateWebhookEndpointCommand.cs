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

public sealed record UpdateWebhookEndpointCommand(
    Guid BoardId,
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

        if (endpoint.BoardId.Value != command.BoardId)
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


