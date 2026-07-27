using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards;

/// <summary>Membership row for a board.</summary>
public sealed class BoardMember : Entity<BoardMemberId>
{
    public BoardId BoardId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public BoardMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    private BoardMember() { }

    private BoardMember(
        BoardMemberId id,
        BoardId boardId,
        Guid userId,
        BoardMemberRole role,
        DateTimeOffset joinedAt)
    {
        Id = id;
        BoardId = boardId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        CreatedAt = joinedAt;
    }

    internal static BoardMember Create(
        BoardId boardId,
        Guid userId,
        BoardMemberRole role,
        DateTimeOffset joinedAt) =>
        new(BoardMemberId.New(), boardId, userId, role, joinedAt);

    internal void SetRole(BoardMemberRole role) => Role = role;
}
