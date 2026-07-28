namespace Cardscape.Domain.Boards;

/// <summary>Identifier of a <see cref="BoardAutomationRule"/>.</summary>
public sealed record BoardAutomationRuleId(Guid Value)
    : Common.GuidId<BoardAutomationRuleId>(Value);
