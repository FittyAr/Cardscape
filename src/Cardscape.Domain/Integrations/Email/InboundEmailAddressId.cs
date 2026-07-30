namespace Cardscape.Domain.Integrations.Email;

public sealed record InboundEmailAddressId(Guid Value) : Common.GuidId<InboundEmailAddressId>(Value);
