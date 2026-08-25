using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using FluentValidation;
using Wolverine;

namespace Cardscape.Application.Automation;

public sealed record BoardAutomationRuleDto(
    Guid Id,
    Guid BoardId,
    string Name,
    int Trigger,
    Guid? TriggerListId,
    int Action,
    string? ActionArgument,
    bool IsEnabled,
    int Position)
{
    public static BoardAutomationRuleDto FromEntity(BoardAutomationRule r) => new(
        r.Id.Value,
        r.BoardId.Value,
        r.Name,
        (int)r.Trigger,
        r.TriggerListId,
        (int)r.Action,
        r.ActionArgument,
        r.IsEnabled,
        r.Position);
}


