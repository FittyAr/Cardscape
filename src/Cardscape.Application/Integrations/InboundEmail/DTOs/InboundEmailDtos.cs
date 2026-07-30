using Cardscape.Domain.Integrations.InboundEmail;

namespace Cardscape.Application.Integrations.InboundEmail.DTOs;

public sealed record InboundEmailAddressDto(
    Guid Id,
    Guid WorkspaceId,
    string EmailAddress,
    Guid TargetListId,
    string Label,
    bool Active,
    DateTimeOffset CreatedAt)
{
    public static InboundEmailAddressDto FromEntity(InboundEmailAddress a) => new(
        a.Id.Value,
        a.WorkspaceId.Value,
        a.EmailAddress,
        a.TargetListId.Value,
        a.Label,
        a.Active,
        a.CreatedAt);
}
