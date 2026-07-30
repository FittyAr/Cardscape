using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Integrations.Email;

/// <summary>
/// A per-workspace inbound email address. When the integration
/// service receives a message at this address, the body is
/// converted into a new card on the configured board.
/// </summary>
public sealed class InboundEmailAddress : AggregateRoot<InboundEmailAddressId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;
    public string Address { get; private set; } = string.Empty;
    public bool Active { get; private set; } = true;

    private InboundEmailAddress() { }

    private InboundEmailAddress(
        InboundEmailAddressId id,
        WorkspaceId workspaceId,
        string address,
        DateTimeOffset at)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Address = address;
        CreatedAt = at;
    }

    public static Result<InboundEmailAddress> Create(
        InboundEmailAddressId id,
        WorkspaceId workspaceId,
        string address,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Result.Failure<InboundEmailAddress>(DomainError.Validation(
                "integrations.email.invalid_address",
                "Inbound email address is required."));
        }

        return Result.Success(new InboundEmailAddress(id, workspaceId, address.Trim(), at));
    }
}
