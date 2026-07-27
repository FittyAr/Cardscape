using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels.Events;

namespace Cardscape.Domain.Labels;

/// <summary>
/// A label is a coloured tag that can be attached to multiple
/// cards on the same board.
/// </summary>
public sealed class Label : AggregateRoot<LabelId>
{
    public BoardId BoardId { get; private set; } = null!;
    public LabelName Name { get; private set; } = null!;
    public Color Color { get; private set; } = null!;

    private Label() { }

    private Label(
        LabelId id,
        BoardId boardId,
        LabelName name,
        Color color,
        Guid createdBy,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Name = name;
        Color = color;
        CreatedBy = createdBy;
        CreatedAt = at;
    }

    public static Result<Label> Create(
        LabelId id,
        BoardId boardId,
        LabelName name,
        Color color,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Label>(DomainError.Validation(
                "labels.creator_required",
                "Label creator is required."));
        }

        var label = new Label(id, boardId, name, color, createdBy, at);
        label.AddDomainEvent(new LabelCreated(id, boardId, name, at));
        return Result.Success(label);
    }

    public Result Update(LabelName newName, Color newColor, DateTimeOffset at)
    {
        var sameName = newName.Value == Name.Value;
        var sameColor = newColor.Value == Color.Value;
        if (sameName && sameColor)
        {
            return Result.Success();
        }

        Name = newName;
        Color = newColor;
        UpdatedAt = at;
        AddDomainEvent(new LabelUpdated(Id, newName, newColor, at));
        return Result.Success();
    }

    public void Delete(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        UpdatedAt = at;
        AddDomainEvent(new LabelDeleted(Id, at));
    }
}
