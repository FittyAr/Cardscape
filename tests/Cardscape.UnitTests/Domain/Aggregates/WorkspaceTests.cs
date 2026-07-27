using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Cardscape.Domain.Workspaces.Errors;
using Cardscape.Domain.Workspaces.Events;

namespace Cardscape.UnitTests.Domain.Aggregates;

public class WorkspaceTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;

    private static Workspace NewWorkspace(Guid? ownerId = null) =>
        Workspace.Create(
            WorkspaceId.New(),
            WorkspaceName.Create("Acme").Value,
            ownerId ?? Guid.NewGuid(),
            At).Value;

    [Fact]
    public void Create_WithValidData_AddsOwnerAsFirstAdmin()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);

        workspace.OwnerId.Should().Be(ownerId);
        workspace.Members.Should().HaveCount(1);
        workspace.Members.First().UserId.Should().Be(ownerId);
        workspace.Members.First().Role.Should().Be(WorkspaceRole.Admin);
        workspace.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyOwnerId_ReturnsValidationFailure()
    {
        var result = Workspace.Create(
            WorkspaceId.New(),
            WorkspaceName.Create("Acme").Value,
            Guid.Empty,
            At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.owner_required");
    }

    [Fact]
    public void Create_RaisesWorkspaceCreatedEvent()
    {
        var workspace = NewWorkspace();

        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceCreated>();
    }

    [Fact]
    public void Rename_WithDifferentName_UpdatesNameAndRaisesEvent()
    {
        var workspace = NewWorkspace();
        workspace.ClearDomainEvents();
        var newName = WorkspaceName.Create("Acme Renamed").Value;

        var result = workspace.Rename(newName, At);

        result.IsSuccess.Should().BeTrue();
        workspace.Name.Value.Should().Be("Acme Renamed");
        workspace.UpdatedAt.Should().Be(At);
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceRenamed>();
    }

    [Fact]
    public void Rename_WithSameName_IsNoop()
    {
        var workspace = NewWorkspace();
        workspace.ClearDomainEvents();
        var sameName = WorkspaceName.Create("Acme").Value;

        var result = workspace.Rename(sameName, At);

        result.IsSuccess.Should().BeTrue();
        workspace.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_WhenArchived_ReturnsForbiddenFailure()
    {
        var workspace = NewWorkspace();
        workspace.Archive(At);
        workspace.ClearDomainEvents();

        var result = workspace.Rename(WorkspaceName.Create("New").Value, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Archive_IsIdempotent()
    {
        var workspace = NewWorkspace();
        workspace.Archive(At);
        workspace.Archive(At);
        workspace.Archive(At);

        workspace.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Archive_RaisesWorkspaceArchivedEvent()
    {
        var workspace = NewWorkspace();
        workspace.ClearDomainEvents();

        workspace.Archive(At);

        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceArchived>();
    }

    [Fact]
    public void AddMember_WithNewUser_AddsMemberAndRaisesEvent()
    {
        var workspace = NewWorkspace();
        var newUser = Guid.NewGuid();
        workspace.ClearDomainEvents();

        var result = workspace.AddMember(newUser, WorkspaceRole.Member, At);

        result.IsSuccess.Should().BeTrue();
        workspace.Members.Should().HaveCount(2);
        workspace.HasMember(newUser).Should().BeTrue();
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceMemberAdded>();
    }

    [Fact]
    public void AddMember_WithExistingUser_ReturnsAlreadyMemberFailure()
    {
        var workspace = NewWorkspace();
        var existingUser = workspace.Members.First().UserId;

        var result = workspace.AddMember(existingUser, WorkspaceRole.Member, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.AlreadyMember.Code);
    }

    [Fact]
    public void AddMember_WhenArchived_ReturnsForbiddenFailure()
    {
        var workspace = NewWorkspace();
        workspace.Archive(At);

        var result = workspace.AddMember(Guid.NewGuid(), WorkspaceRole.Member, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void RemoveMember_WithNonOwnerMember_RemovesAndRaisesEvent()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);
        var otherUser = Guid.NewGuid();
        workspace.AddMember(otherUser, WorkspaceRole.Member, At);
        workspace.ClearDomainEvents();

        var result = workspace.RemoveMember(otherUser, At);

        result.IsSuccess.Should().BeTrue();
        workspace.HasMember(otherUser).Should().BeFalse();
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceMemberRemoved>();
    }

    [Fact]
    public void RemoveMember_OfOwner_ReturnsCannotRemoveOwnerFailure()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);

        var result = workspace.RemoveMember(ownerId, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.CannotRemoveOwner.Code);
    }

    [Fact]
    public void RemoveMember_WithNonExistingUser_ReturnsNotMemberFailure()
    {
        var workspace = NewWorkspace();

        var result = workspace.RemoveMember(Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.NotMember.Code);
    }

    [Fact]
    public void ChangeMemberRole_PromotesMemberToAdmin()
    {
        var workspace = NewWorkspace();
        var user = Guid.NewGuid();
        workspace.AddMember(user, WorkspaceRole.Member, At);
        workspace.ClearDomainEvents();

        var result = workspace.ChangeMemberRole(user, WorkspaceRole.Admin, At);

        result.IsSuccess.Should().BeTrue();
        workspace.Members.First(m => m.UserId == user).Role.Should().Be(WorkspaceRole.Admin);
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceMemberRoleChanged>();
    }

    [Fact]
    public void ChangeMemberRole_OnOwner_Fails()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);

        var result = workspace.ChangeMemberRole(ownerId, WorkspaceRole.Member, At);

        result.IsFailure.Should().BeTrue();
    }
}
