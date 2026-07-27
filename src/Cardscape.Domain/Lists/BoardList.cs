using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists.Errors;
using Cardscape.Domain.Lists.Events;
using static Cardscape.Domain.Lists.Errors.ListErrors;

namespace Cardscape.Domain.Lists;

/// <summary>
/// A list is a column inside a board that groups cards.
/// </summary>
public sealed class BoardList : AggregateRoot<BoardListId>
{
    public BoardId BoardId { get; private set; } = null!;
    public ListName Name { get; private set; } = null!;
    public Position Position { get; private set; }
    public bool IsArchived { get; private set; }

    private BoardList() { }

    private BoardList(
        BoardListId id,
        BoardId boardId,
        ListName name,
        Position position,
        Guid createdBy,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Name = name;
        Position = position;
        CreatedBy = createdBy;
        CreatedAt = at;
    }

    public static Result<BoardList> Create(
        BoardListId id,
        BoardId boardId,
        ListName name,
        Position position,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (createdBy == Guid.Empty)
        {
            return Result.Failure<BoardList>(DomainError.Validation(
                "lists.creator_required",
                "List creator is required."));
        }

        var list = new BoardList(id, boardId, name, position, createdBy, at);
        list.AddDomainEvent(new ListCreated(id, boardId, name, at));
        return Result.Success(list);
    }

    public Result Rename(ListName newName, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(ListErrors.Archived);
        }

        if (newName.Value == Name.Value)
        {
            return Result.Success();
        }

        Name = newName;
        UpdatedAt = at;
        AddDomainEvent(new ListRenamed(Id, newName, at));
        return Result.Success();
    }

    public Result Move(Position newPosition, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(ListErrors.Archived);
        }

        if (Math.Abs(newPosition.Value - Position.Value) < double.Epsilon)
        {
            return Result.Success();
        }

        Position = newPosition;
        UpdatedAt = at;
        AddDomainEvent(new ListMoved(Id, newPosition, at));
        return Result.Success();
    }

    public void Archive(DateTimeOffset at)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        UpdatedAt = at;
        AddDomainEvent(new ListArchived(Id, at));
    }

    public void Restore(DateTimeOffset at)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        UpdatedAt = at;
        AddDomainEvent(new ListRestored(Id, at));
    }
}
