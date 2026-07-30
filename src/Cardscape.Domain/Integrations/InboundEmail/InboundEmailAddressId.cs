namespace Cardscape.Domain.Integrations.InboundEmail;

/// <summary>Identifier of an <see cref="InboundEmailAddress"/>.</summary>
public sealed record InboundEmailAddressId(Guid Value) : Common.GuidId<InboundEmailAddressId>(Value);
