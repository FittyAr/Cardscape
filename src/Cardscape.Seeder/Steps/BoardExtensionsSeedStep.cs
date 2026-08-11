using Cardscape.Domain.Boards;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Enables the four board extensions on every
/// board: custom fields, voting, card repeater, and card
/// aging. The aggregate's <c>Enable</c> factory validates
/// the config JSON so a malformed blob never reaches the
/// table.</summary>
internal sealed class BoardExtensionsSeedStep : SeedStepBase
{
    public override string Name => "Board extensions + automation + custom fields";
    public override int Order => 40;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        int rules = 0;
        int defs = 0;

        foreach (Board board in context.Boards)
        {
            // Custom Fields
            Result<BoardExtension> cf = BoardExtension.Enable(
                board.Id, ExtensionKind.CustomFields, "{\"required\":false}", now);
            if (cf.IsSuccess)
            {
                context.Db.BoardExtensions.Add(cf.Value);
                context.BoardExtensions.Add(cf.Value);
            }

            // Voting
            Result<BoardExtension> voting = BoardExtension.Enable(
                board.Id, ExtensionKind.Voting, "{\"limitPerUser\":1}", now);
            if (voting.IsSuccess)
            {
                context.Db.BoardExtensions.Add(voting.Value);
                context.BoardExtensions.Add(voting.Value);
            }

            // Card Repeater
            Result<BoardExtension> repeater = BoardExtension.Enable(
                board.Id, ExtensionKind.CardRepeater, "{\"defaultIntervalDays\":14}", now);
            if (repeater.IsSuccess)
            {
                context.Db.BoardExtensions.Add(repeater.Value);
                context.BoardExtensions.Add(repeater.Value);
            }

            // Card Aging
            Result<BoardExtension> aging = BoardExtension.Enable(
                board.Id, ExtensionKind.CardAging, "{\"mode\":\"ByActivity\",\"staleAfterDays\":21}", now);
            if (aging.IsSuccess)
            {
                context.Db.BoardExtensions.Add(aging.Value);
                context.BoardExtensions.Add(aging.Value);
            }

            // Custom field definitions on every board: Priority
            // (dropdown), Effort (number), Due Window (date).
            Result<CustomFieldDefinition> priority = CustomFieldDefinition.Create(
                board.Id, "Priority", CustomFieldKind.Dropdown,
                new[] { "Low", "Medium", "High", "Critical" }, 0, now);
            if (priority.IsSuccess)
            {
                context.Db.CustomFieldDefinitions.Add(priority.Value);
                context.CustomFieldDefinitions.Add(priority.Value);
                defs++;
            }

            Result<CustomFieldDefinition> effort = CustomFieldDefinition.Create(
                board.Id, "Effort (points)", CustomFieldKind.Number, null, 1, now);
            if (effort.IsSuccess)
            {
                context.Db.CustomFieldDefinitions.Add(effort.Value);
                context.CustomFieldDefinitions.Add(effort.Value);
                defs++;
            }

            Result<CustomFieldDefinition> dueWindow = CustomFieldDefinition.Create(
                board.Id, "Due Window", CustomFieldKind.Date, null, 2, now);
            if (dueWindow.IsSuccess)
            {
                context.Db.CustomFieldDefinitions.Add(dueWindow.Value);
                context.CustomFieldDefinitions.Add(dueWindow.Value);
                defs++;
            }
        }

        // One automation rule per board: "when a card lands in
        // the Doing list, mark it complete". A bit tongue-in-cheek
        // but exercises the dispatcher and the audit log.
        foreach (Board board in context.Boards)
        {
            Result<BoardAutomationRule> rule = BoardAutomationRule.Create(
                board.Id,
                "Auto-complete on move to Done",
                AutomationTrigger.CardCreatedInList,
                triggerListId: Guid.Empty, // will be re-bound below once lists exist
                AutomationAction.MarkComplete,
                actionArgument: null,
                position: 0,
                at: now);
            if (rule.IsFailure)
            {
                continue;
            }

            context.Add(rule.Value);
            context.AutomationRules.Add(rule.Value);
            rules++;
        }

        // One automation rule per board: "when a card lands in
        // the Doing list, mark it complete". A bit tongue-in-cheek
        // but exercises the dispatcher and the audit log.
        foreach (Board board in context.Boards)
        {
            Result<BoardAutomationRule> rule = BoardAutomationRule.Create(
                board.Id,
                "Auto-complete on move to Done",
                AutomationTrigger.CardCreatedInList,
                triggerListId: Guid.Empty, // will be re-bound below once lists exist
                AutomationAction.MarkComplete,
                actionArgument: null,
                position: 0,
                at: now);
            if (rule.IsFailure)
            {
                continue;
            }

            context.Add(rule.Value);
            context.AutomationRules.Add(rule.Value);
            rules++;
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.BoardExtensions.Count} extension rows, {defs} custom field definitions, and {rules} automation rules.");
        return Task.CompletedTask;
    }
}
