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


