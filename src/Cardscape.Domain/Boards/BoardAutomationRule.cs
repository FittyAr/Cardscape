using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>
/// Trigger conditions a rule can react to. New triggers can be
/// added without touching the dispatcher by extending
/// <c>AutomationTriggerExtensions</c>.
/// </summary>
public enum AutomationTrigger
{
    /// <summary>Any card on the board is moved to a new list.</summary>
    CardMoved = 0,

    /// <summary>Any card on the board is completed.</summary>
    CardCompleted = 1,

    /// <summary>Any card on the board is reopened.</summary>
    CardReopened = 2,

    /// <summary>A card is added to a specific list (matches the configured list id).</summary>
    CardCreatedInList = 3
}

/// <summary>
/// Side-effects a rule can take. Each action has a single
/// <c>Argument</c> (the target list id, the assignee user id, or
/// the label id, depending on the kind). The dispatcher is
/// responsible for resolving the argument to the right entity.
/// </summary>
public enum AutomationAction
{
    /// <summary>Move the matching card to the list whose id is in <c>Argument</c>.</summary>
    MoveCardToList = 0,

    /// <summary>Assign the matching card to the user whose id is in <c>Argument</c>.</summary>
    AssignUser = 1,

    /// <summary>Set the matching card's due date to the absolute timestamp in <c>Argument</c>.</summary>
    SetDueDate = 2,

    /// <summary>Mark the matching card complete.</summary>
    MarkComplete = 3
}

/// <summary>
/// Per-board automation rule. The aggregate owns its trigger
/// configuration (e.g. which list to watch) and its action
/// configuration (e.g. which list to move the card to).
/// </summary>
public sealed class BoardAutomationRule : AggregateRoot<BoardAutomationRuleId>
{
    public BoardId BoardId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public AutomationTrigger Trigger { get; private set; }
    public Guid? TriggerListId { get; private set; }
    public AutomationAction Action { get; private set; }
    public string? ActionArgument { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public int Position { get; private set; }

    // EF Core.
    private BoardAutomationRule() { }

    private BoardAutomationRule(
        BoardAutomationRuleId id,
        BoardId boardId,
        string name,
        AutomationTrigger trigger,
        Guid? triggerListId,
        AutomationAction action,
        string? actionArgument,
        bool isEnabled,
        int position,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Name = name;
        Trigger = trigger;
        TriggerListId = triggerListId;
        Action = action;
        ActionArgument = actionArgument;
        IsEnabled = isEnabled;
        Position = position;
        CreatedAt = at;
    }

    public static Result<BoardAutomationRule> Create(
        BoardId boardId,
        string name,
        AutomationTrigger trigger,
        Guid? triggerListId,
        AutomationAction action,
        string? actionArgument,
        int position,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<BoardAutomationRule>(DomainError.Validation(
                "automation.name_required", "Rule name is required."));
        }

        if (name.Length > 120)
        {
            return Result.Failure<BoardAutomationRule>(DomainError.Validation(
                "automation.name_too_long", "Rule name must be 120 characters or fewer."));
        }

        // BETA-7-#8 — see test-results/BETA-TEST-REPORT.md.
        // The JSON deserialiser happily accepts any int for
        // an enum, so `trigger: 99` and `action: 99` would
        // create a rule that the dispatcher can never fire.
        // Range-check the enums explicitly so the caller
        // gets a clear 400 instead of a silent zombie rule.
        if (!Enum.IsDefined(typeof(AutomationTrigger), trigger))
        {
            return Result.Failure<BoardAutomationRule>(DomainError.Validation(
                "automation.trigger_invalid",
                "Trigger value is not a recognised AutomationTrigger."));
        }

        if (!Enum.IsDefined(typeof(AutomationAction), action))
        {
            return Result.Failure<BoardAutomationRule>(DomainError.Validation(
                "automation.action_invalid",
                "Action value is not a recognised AutomationAction."));
        }

        if (trigger == AutomationTrigger.CardCreatedInList && triggerListId is null)
        {
            return Result.Failure<BoardAutomationRule>(DomainError.Validation(
                "automation.trigger_list_required",
                "CardCreatedInList requires a target list id."));
        }

        if (action == AutomationAction.MoveCardToList && string.IsNullOrWhiteSpace(actionArgument))
        {
            return Result.Failure<BoardAutomationRule>(DomainError.Validation(
                "automation.move_target_required",
                "MoveCardToList requires a target list id."));
        }

        return Result.Success(new BoardAutomationRule(
            BoardAutomationRuleId.New(),
            boardId,
            name.Trim(),
            trigger,
            triggerListId,
            action,
            actionArgument,
            isEnabled: true,
            position: position,
            at: at));
    }

    public Result Rename(string newName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result.Failure(DomainError.Validation(
                "automation.name_required", "Rule name is required."));
        }

        if (newName.Length > 120)
        {
            return Result.Failure(DomainError.Validation(
                "automation.name_too_long", "Rule name must be 120 characters or fewer."));
        }

        Name = newName.Trim();
        StampChanged(by: null, at: at);
        return Result.Success();
    }

    public void Enable(DateTimeOffset at) { IsEnabled = true; StampChanged(by: null, at: at); }
    public void Disable(DateTimeOffset at) { IsEnabled = false; StampChanged(by: null, at: at); }
}
