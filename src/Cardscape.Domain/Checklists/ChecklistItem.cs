using Cardscape.Domain.Common;

namespace Cardscape.Domain.Checklists;

/// <summary>Single line item inside a <see cref="Checklist"/>.</summary>
public sealed class ChecklistItem : Entity<ChecklistItemId>
{
    public ChecklistId ChecklistId { get; private set; } = null!;
    public ChecklistItemText Text { get; private set; } = null!;
    public bool IsCompleted { get; private set; }
    public Position Position { get; private set; }
    public Guid? AssignedTo { get; private set; }

    private ChecklistItem() { }

    private ChecklistItem(
        ChecklistItemId id,
        ChecklistId checklistId,
        ChecklistItemText text,
        Position position,
        DateTimeOffset at)
    {
        Id = id;
        ChecklistId = checklistId;
        Text = text;
        Position = position;
        IsCompleted = false;
        CreatedAt = at;
    }

    internal static ChecklistItem Create(
        ChecklistId checklistId,
        ChecklistItemText text,
        Position position,
        DateTimeOffset at) =>
        new(ChecklistItemId.New(), checklistId, text, position, at);

    /// <summary>Marks the item as done.</summary>
    internal void Check(DateTimeOffset at)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        UpdatedAt = at;
    }

    /// <summary>Marks the item as not done.</summary>
    internal void Uncheck(DateTimeOffset at)
    {
        if (!IsCompleted)
        {
            return;
        }

        IsCompleted = false;
        UpdatedAt = at;
    }

    internal void UpdateText(ChecklistItemText newText, DateTimeOffset at)
    {
        if (newText == Text)
        {
            return;
        }

        Text = newText;
        UpdatedAt = at;
    }
}
