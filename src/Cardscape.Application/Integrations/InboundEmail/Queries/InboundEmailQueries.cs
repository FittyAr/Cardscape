using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.InboundEmail.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.InboundEmail.Queries;

public sealed record ListInboundEmailAddressesQuery(Guid WorkspaceId) : IMessage;

public static class ListInboundEmailAddressesQueryHandler
{
    public static async Task<Result<IReadOnlyList<InboundEmailAddressDto>>> Handle(
        ListInboundEmailAddressesQuery query,
        IInboundEmailAddressRepository addresses,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<InboundEmailAddressDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetWithMembersAsync(
            new WorkspaceId(query.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<IReadOnlyList<InboundEmailAddressDto>>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<InboundEmailAddressDto>>(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        IReadOnlyList<InboundEmailAddress> rows =
            await addresses.ListForWorkspaceAsync(new WorkspaceId(query.WorkspaceId), ct);
        return Result.Success<IReadOnlyList<InboundEmailAddressDto>>(
            rows.Select(InboundEmailAddressDto.FromEntity).ToList());
    }
}
