namespace Cardscape.Web.Shared;

// ── Automation (v0.6.3) ────────────────────────────────
public sealed record BoardAutomationRuleDto(
    Guid Id,
    Guid BoardId,
    string Name,
    AutomationTrigger Trigger,
    Guid? TriggerListId,
    AutomationAction Action,
    string? ActionArgument,
    bool IsEnabled,
    int Position);

public sealed record CreateRuleRequestDto(
    string Name,
    AutomationTrigger Trigger,
    Guid? TriggerListId,
    AutomationAction Action,
    string? ActionArgument,
    int Position = 0);
