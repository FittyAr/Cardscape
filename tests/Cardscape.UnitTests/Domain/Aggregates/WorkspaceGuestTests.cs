using Cardscape.Domain.Boards;

namespace Cardscape.UnitTests.Domain.Aggregates;

/// <summary>
/// Workspace guests: the role is appended (numeric value 3), the
/// aggregate reports guests apart from full members, a guest can never
/// hold the board Admin role, and only full members open
/// workspace-visible boards without explicit membership.
/// </summary>
public sealed class WorkspaceGuestTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static Workspace NewWorkspace(Guid ownerId) =>
        Workspace.Create(WorkspaceId.New(), WorkspaceName.Create("Guests").Value, ownerId, Region.Unspecified, At).Value;

    private static Board NewBoard(Workspace workspace, Guid creatorId, BoardVisibility visibility) =>
        Board.Create(BoardId.New(), workspace.Id, BoardName.Create("Board").Value,
            BoardDescription.Create("d").Value, visibility, creatorId, At).Value;

    [Fact]
    public void Guest_IsAppendedWithoutRenumberingTheOtherRoles()
    {
        ((int)WorkspaceRole.Admin).Should().Be(0);
        ((int)WorkspaceRole.Member).Should().Be(1);
        ((int)WorkspaceRole.Observer).Should().Be(2);
        ((int)WorkspaceRole.Guest).Should().Be(3);
    }

    [Fact]
    public void Workspace_TellsGuestsApartFromFullMembers()
    {
        Guid owner = Guid.NewGuid(), member = Guid.NewGuid(), guest = Guid.NewGuid(), stranger = Guid.NewGuid();
        Workspace workspace = NewWorkspace(owner);
        workspace.AddMember(member, WorkspaceRole.Observer, At);
        workspace.AddMember(guest, WorkspaceRole.Guest, At);

        workspace.RoleOf(guest).Should().Be(WorkspaceRole.Guest);
        workspace.RoleOf(stranger).Should().BeNull();
        workspace.IsGuest(guest).Should().BeTrue();
        workspace.IsGuest(member).Should().BeFalse();
        workspace.HasMember(guest).Should().BeTrue("guests are workspace members");
        workspace.HasFullMembership(guest).Should().BeFalse();
        workspace.HasFullMembership(member).Should().BeTrue();
        workspace.HasFullMembership(owner).Should().BeTrue();
        workspace.HasFullMembership(stranger).Should().BeFalse();
        workspace.CanManageMembers(guest).Should().BeFalse();
    }

    [Fact]
    public void Owner_CannotBeTurnedIntoAGuest()
    {
        Guid owner = Guid.NewGuid();
        Workspace workspace = NewWorkspace(owner);

        workspace.ChangeMemberRole(owner, WorkspaceRole.Guest, At).IsFailure.Should().BeTrue();
        workspace.RoleOf(owner).Should().Be(WorkspaceRole.Admin);
    }

    [Theory]
    [InlineData(WorkspaceRole.Guest, BoardMemberRole.Admin, false)]
    [InlineData(WorkspaceRole.Guest, BoardMemberRole.Member, true)]
    [InlineData(WorkspaceRole.Guest, BoardMemberRole.Observer, true)]
    [InlineData(WorkspaceRole.Member, BoardMemberRole.Admin, true)]
    [InlineData(WorkspaceRole.Observer, BoardMemberRole.Admin, true)]
    public void BoardRoles_AreCappedAtMemberForGuests(WorkspaceRole workspaceRole, BoardMemberRole boardRole, bool allowed)
    {
        WorkspaceGuestRules.AllowsBoardRole(workspaceRole, boardRole).Should().Be(allowed);
        WorkspaceGuestRules.MaxBoardRole.Should().Be(BoardMemberRole.Member);
    }

    [Fact]
    public void WorkspaceVisibleBoards_AreOpenToFullMembersButNotToGuestsOrOutsiders()
    {
        Guid owner = Guid.NewGuid(), member = Guid.NewGuid(), guest = Guid.NewGuid(), outsider = Guid.NewGuid();
        Workspace workspace = NewWorkspace(owner);
        workspace.AddMember(member, WorkspaceRole.Member, At);
        workspace.AddMember(guest, WorkspaceRole.Guest, At);
        Board board = NewBoard(workspace, owner, BoardVisibility.Workspace);

        WorkspaceGuestRules.CanOpenBoard(board, member, workspace.RoleOf(member)).Should().BeTrue();
        WorkspaceGuestRules.CanOpenBoard(board, guest, workspace.RoleOf(guest)).Should().BeFalse();
        WorkspaceGuestRules.CanOpenBoard(board, outsider, workspace.RoleOf(outsider)).Should().BeFalse();

        board.AddMember(guest, BoardMemberRole.Member, At);
        WorkspaceGuestRules.CanOpenBoard(board, guest, workspace.RoleOf(guest)).Should().BeTrue("explicit members always open the board");
    }

    [Fact]
    public void PublicBoards_StayOpenToGuests_PrivateBoardsNeedMembership()
    {
        Guid owner = Guid.NewGuid(), guest = Guid.NewGuid();
        Workspace workspace = NewWorkspace(owner);
        workspace.AddMember(guest, WorkspaceRole.Guest, At);

        WorkspaceGuestRules.CanOpenBoard(NewBoard(workspace, owner, BoardVisibility.Public), guest, WorkspaceRole.Guest)
            .Should().BeTrue();
        WorkspaceGuestRules.CanOpenBoard(NewBoard(workspace, owner, BoardVisibility.Private), guest, WorkspaceRole.Guest)
            .Should().BeFalse();
    }
}
