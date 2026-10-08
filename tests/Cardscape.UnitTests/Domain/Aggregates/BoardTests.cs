using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Boards.Events;
using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.Aggregates;

public sealed class BoardTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;

    private static Board NewBoard(Guid? creatorId = null) =>
        Board.Create(
            BoardId.New(),
            WorkspaceId.New(),
            BoardName.Create("My Board").Value,
            BoardDescription.Create("desc").Value,
            BoardVisibility.Private,
            creatorId ?? Guid.NewGuid(),
            At).Value;

    [Fact]
    public void Create_WithValidData_AddsCreatorAsFirstAdminMember()
    {
        var creatorId = Guid.NewGuid();
        var board = NewBoard(creatorId);

        board.Members.Should().HaveCount(1);
        board.Members.First().UserId.Should().Be(creatorId);
        board.Members.First().Role.Should().Be(BoardMemberRole.Admin);
        board.IsStarredBy(creatorId).Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyCreatorId_ReturnsValidationFailure()
    {
        var result = Board.Create(
            BoardId.New(),
            WorkspaceId.New(),
            BoardName.Create("X").Value,
            BoardDescription.Create("d").Value,
            BoardVisibility.Private,
            Guid.Empty,
            At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.creator_required");
    }

    [Fact]
    public void Create_RaisesBoardCreatedEvent()
    {
        var board = NewBoard();

        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardCreated>();
    }

    [Fact]
    public void Rename_WithDifferentName_UpdatesAndRaisesEvent()
    {
        var board = NewBoard();
        board.ClearDomainEvents();
        var newName = BoardName.Create("Renamed").Value;

        var result = board.Rename(newName, At);

        result.IsSuccess.Should().BeTrue();
        board.Name.Value.Should().Be("Renamed");
        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardRenamed>();
    }

    [Fact]
    public void Rename_WithSameName_IsNoop()
    {
        var board = NewBoard();
        board.ClearDomainEvents();

        var result = board.Rename(BoardName.Create("My Board").Value, At);

        result.IsSuccess.Should().BeTrue();
        board.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_WhenArchived_ReturnsArchivedFailure()
    {
        var board = NewBoard();
        board.Archive(At);

        var result = board.Rename(BoardName.Create("New").Value, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.Archived.Code);
    }

    [Fact]
    public void ChangeDescription_WhenArchived_Fails()
    {
        var board = NewBoard();
        board.Archive(At);

        var result = board.ChangeDescription(BoardDescription.Create("d").Value, At);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ChangeVisibility_RaisesEvent()
    {
        var board = NewBoard();
        board.ClearDomainEvents();

        board.ChangeVisibility(BoardVisibility.Public, At);

        board.Visibility.Should().Be(BoardVisibility.Public);
        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardVisibilityChanged>();
    }

    [Fact]
    public void Archive_IsIdempotent()
    {
        var board = NewBoard();
        board.Archive(At);
        board.Archive(At);
        board.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Unarchive_AfterArchive_Restores()
    {
        var board = NewBoard();
        board.Archive(At);

        board.Unarchive(At);

        board.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Star_WithNewUser_AddsStarAndRaisesEvent()
    {
        var board = NewBoard();
        var userId = Guid.NewGuid();
        board.ClearDomainEvents();

        var result = board.Star(userId, At);

        result.IsSuccess.Should().BeTrue();
        board.IsStarredBy(userId).Should().BeTrue();
        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardStarred>();
    }

    [Fact]
    public void Star_WithSameUserTwice_IsIdempotent()
    {
        var board = NewBoard();
        var userId = Guid.NewGuid();

        board.Star(userId, At);
        board.Star(userId, At);
        board.Star(userId, At);

        board.Stars.Should().HaveCount(1);
    }

    [Fact]
    public void Unstar_AfterStar_RemovesAndRaisesEvent()
    {
        var board = NewBoard();
        var userId = Guid.NewGuid();
        board.Star(userId, At);
        board.ClearDomainEvents();

        var result = board.Unstar(userId, At);

        result.IsSuccess.Should().BeTrue();
        board.IsStarredBy(userId).Should().BeFalse();
        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardUnstarred>();
    }

    [Fact]
    public void Unstar_WithoutPriorStar_IsNoop()
    {
        var board = NewBoard();

        var result = board.Unstar(Guid.NewGuid(), At);

        result.IsSuccess.Should().BeTrue();
        board.Stars.Should().BeEmpty();
    }

    [Fact]
    public void AddMember_WhenArchived_ReturnsArchivedFailure()
    {
        var board = NewBoard();
        board.Archive(At);

        var result = board.AddMember(Guid.NewGuid(), BoardMemberRole.Member, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.Archived.Code);
    }

    [Fact]
    public void AddMember_WithExistingMember_ReturnsAlreadyMemberFailure()
    {
        var board = NewBoard();
        var creatorId = board.Members.First().UserId;

        var result = board.AddMember(creatorId, BoardMemberRole.Member, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.AlreadyMember.Code);
    }

    [Fact]
    public void AddMember_WithNewUser_AddsAndRaisesEvent()
    {
        var board = NewBoard();
        var userId = Guid.NewGuid();
        board.ClearDomainEvents();

        var result = board.AddMember(userId, BoardMemberRole.Member, At);

        result.IsSuccess.Should().BeTrue();
        board.IsMember(userId).Should().BeTrue();
        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardMemberAdded>();
    }

    [Fact]
    public void RemoveMember_OfLastAdmin_ReturnsLastAdminFailure()
    {
        var board = NewBoard();    // creator is the only admin
        var creatorId = board.Members.First().UserId;

        var result = board.RemoveMember(creatorId, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.LastAdmin.Code);
    }

    [Fact]
    public void RemoveMember_OfAdminWhenAnotherAdminExists_Succeeds()
    {
        var board = NewBoard();
        var creatorId = board.Members.First().UserId;
        var newAdmin = Guid.NewGuid();
        board.AddMember(newAdmin, BoardMemberRole.Admin, At);

        var result = board.RemoveMember(creatorId, At);

        result.IsSuccess.Should().BeTrue();
        board.Members.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveMember_OfNonExisting_ReturnsMemberNotFoundFailure()
    {
        var board = NewBoard();

        var result = board.RemoveMember(Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.members.not_found");
    }

    [Fact]
    public void RemoveMember_OfLastAdmin_UsesTheBoardMembersErrorCode()
    {
        var board = NewBoard();
        board.AddMember(Guid.NewGuid(), BoardMemberRole.Member, At);

        var result = board.RemoveMember(board.Members.First().UserId, At);

        result.Error.Code.Should().Be("boards.members.last_admin");
        board.Members.Should().HaveCount(2);
    }

    [Fact]
    public void ChangeMemberRole_PromotesMemberAndRaisesEvent()
    {
        var board = NewBoard();
        var userId = Guid.NewGuid();
        board.AddMember(userId, BoardMemberRole.Observer, At);
        board.ClearDomainEvents();

        var result = board.ChangeMemberRole(userId, BoardMemberRole.Admin, At);

        result.IsSuccess.Should().BeTrue();
        board.IsAdmin(userId).Should().BeTrue();
        board.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BoardMemberRoleChanged>()
            .Which.Role.Should().Be(BoardMemberRole.Admin);
    }

    [Fact]
    public void ChangeMemberRole_DemotingTheLastAdmin_IsRefused()
    {
        var board = NewBoard();
        var creatorId = board.Members.First().UserId;
        board.AddMember(Guid.NewGuid(), BoardMemberRole.Member, At);

        var result = board.ChangeMemberRole(creatorId, BoardMemberRole.Member, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.LastAdmin.Code);
        board.IsAdmin(creatorId).Should().BeTrue();
    }

    [Fact]
    public void ChangeMemberRole_DemotingAnAdminWhenAnotherRemains_Succeeds()
    {
        var board = NewBoard();
        var creatorId = board.Members.First().UserId;
        board.AddMember(Guid.NewGuid(), BoardMemberRole.Admin, At);

        var result = board.ChangeMemberRole(creatorId, BoardMemberRole.Observer, At);

        result.IsSuccess.Should().BeTrue();
        board.Members.Single(m => m.UserId == creatorId).Role.Should().Be(BoardMemberRole.Observer);
    }

    [Fact]
    public void ChangeMemberRole_ToTheSameRole_IsANoOp()
    {
        var board = NewBoard();
        var creatorId = board.Members.First().UserId;
        board.ClearDomainEvents();

        var result = board.ChangeMemberRole(creatorId, BoardMemberRole.Admin, At);

        result.IsSuccess.Should().BeTrue();
        board.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeMemberRole_OfNonMember_ReturnsMemberNotFound()
    {
        var board = NewBoard();

        var result = board.ChangeMemberRole(Guid.NewGuid(), BoardMemberRole.Member, At);

        result.Error.Code.Should().Be(BoardErrors.MemberNotFound.Code);
    }

    [Fact]
    public void ChangeMemberRole_WithUndefinedRole_IsRefused()
    {
        var board = NewBoard();
        var userId = Guid.NewGuid();
        board.AddMember(userId, BoardMemberRole.Member, At);

        var result = board.ChangeMemberRole(userId, (BoardMemberRole)42, At);

        result.Error.Code.Should().Be(BoardErrors.InvalidMemberRole.Code);
    }

    [Fact]
    public void ChangeColor_SetsColorAndRaisesEvent()
    {
        var board = NewBoard();
        board.ClearDomainEvents();

        var result = board.ChangeColor(Color.Palette.Blue, At);

        result.IsSuccess.Should().BeTrue();
        board.Color.Should().Be(Color.Palette.Blue);
        board.DomainEvents.OfType<BoardColorChanged>().Should().ContainSingle()
            .Which.NewColor.Should().Be(Color.Palette.Blue);
    }

    [Fact]
    public void ChangeColor_ToSameColor_IsNoOp()
    {
        var board = NewBoard();
        board.ChangeColor(Color.Palette.Green, At);
        board.ClearDomainEvents();

        board.ChangeColor(Color.Palette.Green, At).IsSuccess.Should().BeTrue();

        board.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeColor_ToNull_ClearsColor()
    {
        var board = NewBoard();
        board.ChangeColor(Color.Palette.Red, At);

        board.ChangeColor(null, At).IsSuccess.Should().BeTrue();

        board.Color.Should().BeNull();
        board.DomainEvents.OfType<BoardColorChanged>().Last().NewColor.Should().BeNull();
    }

    [Fact]
    public void ChangeColor_WhenArchived_ReturnsArchivedFailure()
    {
        var board = NewBoard();
        board.Archive(At);

        var result = board.ChangeColor(Color.Palette.Blue, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.Archived.Code);
        board.Color.Should().BeNull();
    }
}
