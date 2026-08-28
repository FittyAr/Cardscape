using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Integrations.InboundEmail.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Integrations.InboundEmail.Commands;

public sealed record RegisterInboundEmailAddressCommand(
    Guid WorkspaceId,
    string EmailAddress,
    Guid TargetListId,
    string Label) : IMessage;

public static class RegisterInboundEmailAddressCommandHandler
{
    public static async Task<Result<InboundEmailAddressDto>> Handle(
        RegisterInboundEmailAddressCommand command,
        IInboundEmailAddressRepository addresses,
        IWorkspaceRepository workspaces,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<InboundEmailAddressDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspaceId = new WorkspaceId(command.WorkspaceId);
        Workspace? workspace = await workspaces.GetWithMembersAsync(workspaceId, ct);
        if (workspace is null)
        {
            return Result.Failure<InboundEmailAddressDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<InboundEmailAddressDto>(DomainError.Forbidden(
                "workspaces.forbidden", "You are not a member of this workspace."));
        }

        var listGuard = await MembershipGuards.EnsureCanMutateListAsync(
            lists, boards, currentUser.Id.Value, command.TargetListId, ct);
        if (listGuard.IsFailure)
        {
            return Result.Failure<InboundEmailAddressDto>(listGuard.Error);
        }

        if (listGuard.Value.Board.WorkspaceId != workspaceId)
        {
            return Result.Failure<InboundEmailAddressDto>(DomainError.Validation(
                "inbound_email.target_list_workspace_mismatch",
                "Target list must belong to the selected workspace."));
        }

        var creation = InboundEmailAddress.Register(
            InboundEmailAddressId.New(),
            workspaceId,
            command.EmailAddress,
            listGuard.Value.List.Id,
            command.Label,
            clock.UtcNow);
        if (creation.IsFailure)
        {
            return Result.Failure<InboundEmailAddressDto>(creation.Error);
        }

        await addresses.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(InboundEmailAddressDto.FromEntity(creation.Value));
    }
}
