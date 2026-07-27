using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>
/// A "star" record: a user has flagged a board as a favourite.
/// Unique by the (board, user) pair.
/// </summary>
public sealed class BoardStar : Entity<Guid>
{
    public BoardId BoardId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTimeOffset StarredAt { get; private set; }

    private BoardStar() { }

    private BoardStar(BoardId boardId, Guid userId, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        BoardId = boardId;
        UserId = userId;
        StarredAt = at;
        CreatedAt = at;
    }

    internal static BoardStar Create(BoardId boardId, Guid userId, DateTimeOffset at) =>
        new(boardId, userId, at);
}
