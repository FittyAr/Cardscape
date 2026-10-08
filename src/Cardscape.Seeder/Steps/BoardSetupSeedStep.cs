using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Configures each board the way its team would: the kanban workflow with
/// WIP limits, an archived list, labels, the extensions its blueprint asks
/// for (with one custom field of every kind), dashcards of every kind and
/// automation rules bound to the real lists and people.
/// </summary>
internal sealed class BoardSetupSeedStep : SeedStepBase
{
    private static readonly (string Name, Color Color)[] StandardLabels =
    [
        ("Urgent", Color.Palette.Red),
        ("Bug", Color.Palette.Orange),
        ("Feature", Color.Palette.Green),
        ("Chore", Color.Palette.Gray),
        ("Docs", Color.Palette.Sky),
        ("Customer", Color.Palette.Purple),
    ];

    private static readonly string[] PriorityOptions = ["Low", "Medium", "High", "Critical"];

    private static readonly (DashcardKind Kind, string Title)[] AllDashcards =
    [
        (DashcardKind.OverdueCount, "Overdue cards"),
        (DashcardKind.DueThisWeek, "Due this week"),
        (DashcardKind.ByList, "Cards per list"),
        (DashcardKind.ByMember, "Cards per person"),
        (DashcardKind.ByLabel, "Cards per label"),
    ];

    public override string Name => "Board setup: lists, labels, extensions, dashboards, automation";
    public override int Order => 40;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        foreach (Board board in context.Boards)
        {
            BoardBlueprint blueprint = Blueprint(board);
            User owner = context.User(blueprint.OwnerKey);
            DateTimeOffset at = context.Timeline.Between(board.CreatedAt, board.CreatedAt.AddHours(4));

            PlantLists(context, board, owner, at);
            PlantLabels(context, board, owner, at);
            PlantExtensions(context, board, blueprint, at);
            PlantDashcards(context, board, owner, at, all: blueprint.Name == "Engineering");
        }

        PlantAutomation(context);

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Lists.Count} lists, {context.Labels.Count} labels, {context.BoardExtensions.Count} extensions, " +
            $"{context.CustomFieldDefinitions.Count} custom fields, {context.Dashcards.Count} dashcards and {context.AutomationRules.Count} automation rules.");
        return Task.CompletedTask;
    }

    internal static BoardBlueprint Blueprint(Board board) =>
        NexoraStudios.Boards
            .Append(NexoraStudios.ArchivedBoard)
            .Append(NexoraStudios.LabsBoard)
            .First(b => b.Name == board.Name.Value);

    private static void PlantLists(SeedContext context, Board board, User owner, DateTimeOffset at)
    {
        for (int i = 0; i < NexoraStudios.Workflow.Count; i++)
        {
            AddList(context, board, owner, NexoraStudios.Workflow[i], i + 1, at);
        }

        // Teams that work in flow cap their WIP; Engineering also keeps an
        // old "Icebox" column it archived a few weeks ago.
        if (board.Name.Value is "Engineering" or "Customer Support")
        {
            context.List(board, "Doing").SetLimit(maxSoft: 4, maxHard: 6, at);
            context.List(board, "Review").SetLimit(maxSoft: 3, maxHard: null, at);
        }

        if (board.Name.Value == "Engineering")
        {
            BoardList icebox = AddList(context, board, owner, "Icebox", NexoraStudios.Workflow.Count + 1, at);
            DateTimeOffset archivedAt = context.Timeline.OnDay(-21);
            icebox.Archive(archivedAt);
            context.RecordActivity(board, null, owner, ActivityKind.ListArchived, archivedAt, new { listId = icebox.Id.Value });
        }
    }

    private static BoardList AddList(SeedContext context, Board board, User owner, string name, int position, DateTimeOffset at)
    {
        BoardList list = BoardList.Create(
            BoardListId.New(), board.Id, ListName.Create(name).Value, Position.From(position), owner.Id.Value, at).Value;
        context.Track(context.Lists, list);
        context.RecordActivity(board, null, owner, ActivityKind.ListCreated, at, new { listId = list.Id.Value, name });
        return list;
    }

    private static void PlantLabels(SeedContext context, Board board, User owner, DateTimeOffset at)
    {
        foreach ((string name, Color color) in StandardLabels)
        {
            Label label = Label.Create(LabelId.New(), board.Id, LabelName.Create(name).Value, color, owner.Id.Value, at).Value;
            context.Track(context.Labels, label);
            context.RecordActivity(board, null, owner, ActivityKind.LabelCreated, at, new { labelId = label.Id.Value, name });
        }
    }

    private static void PlantExtensions(SeedContext context, Board board, BoardBlueprint blueprint, DateTimeOffset at)
    {
        if (blueprint.Has(BoardFeatures.Voting))
        {
            Enable(context, board, ExtensionKind.Voting, """{"limitPerUser":1}""", at);
        }

        if (blueprint.Has(BoardFeatures.Repeater))
        {
            Enable(context, board, ExtensionKind.CardRepeater, """{"defaultIntervalDays":30}""", at);
        }

        if (blueprint.Has(BoardFeatures.Aging))
        {
            Enable(context, board, ExtensionKind.CardAging, """{"mode":"ByActivity"}""", at);
        }

        if (!blueprint.Has(BoardFeatures.CustomFields))
        {
            return;
        }

        Enable(context, board, ExtensionKind.CustomFields, """{"required":false}""", at);
        (string Name, CustomFieldKind Kind, IReadOnlyList<string>? Options)[] fields =
        [
            ("Priority", CustomFieldKind.Dropdown, PriorityOptions),
            ("Story points", CustomFieldKind.Number, null),
            ("Target date", CustomFieldKind.Date, null),
            ("Customer facing", CustomFieldKind.Checkbox, null),
            ("Spec link", CustomFieldKind.Text, null),
        ];
        for (int i = 0; i < fields.Length; i++)
        {
            CustomFieldDefinition definition = CustomFieldDefinition.Create(
                board.Id, fields[i].Name, fields[i].Kind, fields[i].Options, i, at).Value;
            context.Track(context.CustomFieldDefinitions, definition);
        }
    }

    private static void Enable(SeedContext context, Board board, ExtensionKind kind, string configJson, DateTimeOffset at)
    {
        BoardExtension extension = BoardExtension.Enable(board.Id, kind, configJson, at).Value;
        context.Track(context.BoardExtensions, extension);
    }

    private static void PlantDashcards(SeedContext context, Board board, User owner, DateTimeOffset at, bool all)
    {
        IEnumerable<(DashcardKind Kind, string Title)> cards = all
            ? AllDashcards
            : [AllDashcards[0], AllDashcards[1], AllDashcards[2]];
        int position = 0;
        foreach ((DashcardKind kind, string title) in cards)
        {
            Dashcard dashcard = Dashcard.Create(DashcardId.New(), board.Id, kind, title, "{}", position++, owner.Id.Value, at).Value;
            context.Add(dashcard);
            context.Dashcards.Add(dashcard);
        }
    }

    /// <summary>Rules a real team would set up, bound to real list and user ids.</summary>
    private static void PlantAutomation(SeedContext context)
    {
        Board engineering = context.Boards.First(b => b.Name.Value == "Engineering");
        Board support = context.Boards.First(b => b.Name.Value == "Customer Support");
        Board operations = context.Boards.First(b => b.Name.Value == "Operations");

        AddRule(context, engineering, "Completed cards move to Done", AutomationTrigger.CardCompleted, null,
            AutomationAction.MoveCardToList, context.List(engineering, "Done").Id.Value.ToString());
        AddRule(context, engineering, "New review cards go to Linus", AutomationTrigger.CardCreatedInList,
            context.List(engineering, "Review").Id.Value, AutomationAction.AssignUser, context.User("linus.pauling").Id.Value.ToString());

        AddRule(context, support, "New tickets are triaged by Selena", AutomationTrigger.CardCreatedInList,
            context.List(support, "Backlog").Id.Value, AutomationAction.AssignUser, context.User("selena.quintero").Id.Value.ToString());
        AddRule(context, support, "Reopened tickets go back to Doing", AutomationTrigger.CardReopened, null,
            AutomationAction.MoveCardToList, context.List(support, "Doing").Id.Value.ToString());

        AddRule(context, operations, "Completed cards move to Done", AutomationTrigger.CardCompleted, null,
            AutomationAction.MoveCardToList, context.List(operations, "Done").Id.Value.ToString());
        BoardAutomationRule paused = AddRule(context, operations, "Moving a card completes it (paused)",
            AutomationTrigger.CardMoved, null, AutomationAction.MarkComplete, null);
        paused.Disable(context.Timeline.OnDay(-10));
    }

    private static BoardAutomationRule AddRule(
        SeedContext context,
        Board board,
        string name,
        AutomationTrigger trigger,
        Guid? triggerListId,
        AutomationAction action,
        string? argument)
    {
        int position = context.AutomationRules.Count(r => r.BoardId == board.Id);
        BoardAutomationRule rule = BoardAutomationRule.Create(
            board.Id, name, trigger, triggerListId, action, argument, position,
            context.Timeline.Between(board.CreatedAt, board.CreatedAt.AddDays(10))).Value;
        context.Add(rule);
        context.AutomationRules.Add(rule);
        return rule;
    }
}
