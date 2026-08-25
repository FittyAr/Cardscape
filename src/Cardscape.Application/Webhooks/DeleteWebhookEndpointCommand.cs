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

public sealed record DeleteWebhookEndpointCommand(Guid BoardId, Guid EndpointId) : IMessage;

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

        if (endpoint.BoardId.Value != command.BoardId)
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


