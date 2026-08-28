using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.InboundEmail.Commands;

public sealed record UnregisterInboundEmailAddressCommand(Guid AddressId) : IMessage;

public static class UnregisterInboundEmailAddressCommandHandler
{
    public static async Task<Result> Handle(
        UnregisterInboundEmailAddressCommand command,
        IInboundEmailAddressRepository addresses,
        IWorkspaceRepository workspaces,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        InboundEmailAddress? address = await addresses.GetByIdAsync(
            new InboundEmailAddressId(command.AddressId), ct);
        if (address is null)
        {
            return Result.Failure(DomainError.NotFound(
                "inbound_email.not_found", "Inbound email address was not found."));
        }

        Workspace? workspace = await workspaces.GetWithMembersAsync(address.WorkspaceId, ct);
        if (workspace is null || !workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        address.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
