namespace Cardscape.Domain.Activities;

/// <summary>Categories of activity event. Used both for the audit log
/// and for rendering timelines.</summary>
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
