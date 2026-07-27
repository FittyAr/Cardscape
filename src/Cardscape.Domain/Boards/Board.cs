using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Boards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Domain.Boards;

/// <summary>
/// A board is the unit of work in Cardscape. A board belongs to
/// exactly one workspace and owns lists, cards, labels, members,
/// and stars.
/// </summary>
public sealed class Board : AggregateRoot<BoardId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;
    public BoardName Name { get; private set; } = null!;
    public BoardDescription Description { get; private set; } = null!;
    public BoardVisibility Visibility { get; private set; }
    public bool IsArchived { get; private set; }

    private readonly List<BoardMember> _members = [];
    public IReadOnlyCollection<BoardMember> Members => _members.AsReadOnly();

    private readonly List<BoardStar> _stars = [];
    public IReadOnlyCollection<BoardStar> Stars => _stars.AsReadOnly();

    private Board() { }

    private Board(
        BoardId id,
        WorkspaceId workspaceId,
        BoardName name,
        BoardDescription description,
        BoardVisibility visibility,
        Guid createdBy,
        DateTimeOffset at)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        Description = description;
        Visibility = visibility;
        CreatedBy = createdBy;
        CreatedAt = at;

        // The creator becomes the first board admin.
        _members.Add(BoardMember.Create(id, createdBy, BoardMemberRole.Admin, at));
    }

    public static Result<Board> Create(
        BoardId id,
        WorkspaceId workspaceId,
        BoardName name,
        BoardDescription description,
        BoardVisibility visibility,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Board>(DomainError.Validation(
                "boards.creator_required",
                "Board creator is required."));
        }

        var board = new Board(id, workspaceId, name, description, visibility, createdBy, at);
        board.AddDomainEvent(new BoardCreated(id, workspaceId, name, at));
        return Result.Success(board);
    }

    public Result Rename(BoardName newName, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(BoardErrors.Archived);
        }

        if (newName.Value == Name.Value)
        {
            return Result.Success();
        }

        Name = newName;
        UpdatedAt = at;
        AddDomainEvent(new BoardRenamed(Id, newName, at));
        return Result.Success();
    }

    public Result ChangeDescription(BoardDescription newDescription, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(BoardErrors.Archived);
        }

        if (newDescription.Value == Description.Value)
        {
            return Result.Success();
        }

        Description = newDescription;
        UpdatedAt = at;
        AddDomainEvent(new BoardDescriptionChanged(Id, newDescription, at));
        return Result.Success();
    }

    public Result ChangeVisibility(BoardVisibility newVisibility, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(BoardErrors.Archived);
        }

        if (newVisibility == Visibility)
        {
            return Result.Success();
        }

        Visibility = newVisibility;
        UpdatedAt = at;
        AddDomainEvent(new BoardVisibilityChanged(Id, newVisibility, at));
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
        AddDomainEvent(new BoardArchived(Id, at));
    }

    public void Unarchive(DateTimeOffset at)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        UpdatedAt = at;
        AddDomainEvent(new BoardUnarchived(Id, at));
    }

    public Result AddMember(Guid userId, BoardMemberRole role, DateTimeOffset at)
    {
        if (IsArchived)
        {
            return Result.Failure(BoardErrors.Archived);
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return Result.Failure(BoardErrors.AlreadyMember);
        }

        _members.Add(BoardMember.Create(Id, userId, role, at));
        UpdatedAt = at;
        AddDomainEvent(new BoardMemberAdded(Id, userId, role, at));
        return Result.Success();
    }

    public Result RemoveMember(Guid userId, DateTimeOffset at)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(BoardErrors.NotMember);
        }

        if (member.Role == BoardMemberRole.Admin
            && _members.Count(m => m.Role == BoardMemberRole.Admin) == 1)
        {
            return Result.Failure(BoardErrors.LastAdmin);
        }

        _members.Remove(member);
        UpdatedAt = at;
        AddDomainEvent(new BoardMemberRemoved(Id, userId, at));
        return Result.Success();
    }

    public Result Star(Guid userId, DateTimeOffset at)
    {
        if (_stars.Any(s => s.UserId == userId))
        {
            return Result.Success();
        }

        _stars.Add(BoardStar.Create(Id, userId, at));
        AddDomainEvent(new BoardStarred(Id, userId, at));
        return Result.Success();
    }

    public Result Unstar(Guid userId, DateTimeOffset at)
    {
        var star = _stars.FirstOrDefault(s => s.UserId == userId);
        if (star is null)
        {
            return Result.Success();
        }

        _stars.Remove(star);
        AddDomainEvent(new BoardUnstarred(Id, userId, at));
        return Result.Success();
    }

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);
    public bool IsStarredBy(Guid userId) => _stars.Any(s => s.UserId == userId);
}
