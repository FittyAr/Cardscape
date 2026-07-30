using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Integrations.InboundEmail.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
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

        var workspace = await workspaces.GetWithMembersAsync(
            new WorkspaceId(command.WorkspaceId), ct);
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

        var list = await lists.GetByIdAsync(new BoardListId(command.TargetListId), ct);
        if (list is null)
        {
            return Result.Failure<InboundEmailAddressDto>(DomainError.NotFound(
                "lists.not_found", "List was not found."));
        }

        if (list.BoardId.Value == Guid.Empty || list.BoardId == default)
        {
            return Result.Failure<InboundEmailAddressDto>(DomainError.Validation(
                "lists.invalid_state", "List is not associated with a board."));
        }

        var creation = InboundEmailAddress.Register(
            InboundEmailAddressId.New(),
            new WorkspaceId(command.WorkspaceId),
            command.EmailAddress,
            new BoardListId(command.TargetListId),
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

        var address = await addresses.GetByIdAsync(
            new InboundEmailAddressId(command.AddressId), ct);
        if (address is null)
        {
            return Result.Failure(DomainError.NotFound(
                "inbound_email.not_found", "Inbound email address was not found."));
        }

        var workspace = await workspaces.GetWithMembersAsync(address.WorkspaceId, ct);
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

public sealed record HandleInboundEmailCommand(
    string Provider,
    string RawBody,
    IDictionary<string, string> Headers) : IMessage;

public static class HandleInboundEmailCommandHandler
{
    public static Task<Result<Guid>> Handle(
        HandleInboundEmailCommand command,
        IInboundEmailService service,
        CancellationToken ct)
    {
        return service.HandleAsync(command.Provider, command.RawBody, command.Headers, ct);
    }
}
