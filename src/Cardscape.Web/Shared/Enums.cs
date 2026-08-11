namespace Cardscape.Web.Shared;

// ── Enums ─────────────────────────────────────────────────
//
// The Blazor WebAssembly client cannot reference the server-side
// `Cardscape.Domain` assembly (the Web project ships standalone with
// no reference to the API), so the enums the Web needs at the
// wire boundary are duplicated here. The numeric values are kept
// in lockstep with the Domain types — see:
//   • src/Cardscape.Domain/Workspaces/Region.cs
//   • src/Cardscape.Domain/Workspaces/WorkspaceRole.cs
//   • src/Cardscape.Domain/Boards/BoardVisibility.cs
//   • src/Cardscape.Domain/Boards/BoardExtension.cs  (ExtensionKind)
//   • src/Cardscape.Domain/Boards/CustomFieldKind.cs
//   • src/Cardscape.Domain/Boards/BoardAutomationRule.cs
//   • src/Cardscape.Domain/Dashboards/DashcardKind.cs
//   • src/Cardscape.Domain/Activities/ActivityKind.cs
//
// The API serialises every enum as a camelCase string
// (e.g. "private", "member", "customFields") via the
// `JsonStringEnumConverter(CamelCase, allowIntegerValues: false)`
// configured in `src/Cardscape.Api/Program.cs:53-58`. The matching
// Web-side options live on `Cardscape.Web.Services.Api.ApiClientBase.JsonOptions`
// so the same strings round-trip cleanly.

// ── Workspaces ────────────────────────────────────────────
public enum Region
{
    Unspecified = 0,
    Europe = 1,
    NorthAmerica = 2,
    AsiaPacific = 3,
    SouthAmerica = 4
}

public enum WorkspaceRole
{
    Admin = 0,
    Member = 1,
    Observer = 2
}

// ── Boards ────────────────────────────────────────────────
public enum BoardVisibility
{
    Private = 0,
    Workspace = 1,
    Public = 2
}

// ── Board extensions ──────────────────────────────────────
public enum BoardExtensionKind
{
    CustomFields = 0,
    Voting = 1,
    CardRepeater = 2,
    CardAging = 3
}

// ── Custom fields ─────────────────────────────────────────
public enum CustomFieldKind
{
    Text = 0,
    Number = 1,
    Date = 2,
    Dropdown = 3,
    Checkbox = 4
}

// ── Automation ────────────────────────────────────────────
public enum AutomationTrigger
{
    CardMoved = 0,
    CardCompleted = 1,
    CardReopened = 2,
    CardCreatedInList = 3
}

public enum AutomationAction
{
    MoveCardToList = 0,
    AssignUser = 1,
    SetDueDate = 2,
    MarkComplete = 3
}

// ── Dashboards (per-board aggregations) ───────────────────
public enum DashcardKind
{
    OverdueCount = 0,
    ByMember = 1,
    ByLabel = 2,
    ByList = 3,
    DueThisWeek = 4
}

public enum ActivityKind
{
    BoardCreated = 0,
    BoardRenamed = 1,
    BoardArchived = 2,
    BoardUnarchived = 3,
    ListCreated = 4,
    ListRenamed = 5,
    ListMoved = 6,
    ListArchived = 7,
    CardCreated = 8,
    CardRenamed = 9,
    CardMoved = 10,
    CardArchived = 11,
    CardRestored = 12,
    CardAssigned = 13,
    CardUnassigned = 14,
    CardDueDateSet = 15,
    CardDueDateCleared = 16,
    LabelAdded = 17,
    LabelRemoved = 18,
    CommentAdded = 19,
    ChecklistCreated = 20,
    ChecklistItemCompleted = 21,
    ChecklistItemUncompleted = 22,
    AttachmentAdded = 23,
    AttachmentRemoved = 24
}
