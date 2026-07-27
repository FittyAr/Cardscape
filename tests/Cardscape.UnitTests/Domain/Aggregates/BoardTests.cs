using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Boards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.UnitTests.Domain.Aggregates;

public class BoardTests
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
    public void RemoveMember_OfNonExisting_ReturnsNotMemberFailure()
    {
        var board = NewBoard();

        var result = board.RemoveMember(Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BoardErrors.NotMember.Code);
    }
}
