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
            Region.Unspecified,
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
            Region.Unspecified,
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

    [Fact]
    public void Create_WithRegion_StoresItOnTheAggregate()
    {
        var workspace = Workspace.Create(
            WorkspaceId.New(),
            WorkspaceName.Create("Acme").Value,
            Guid.NewGuid(),
            Region.Europe,
            At).Value;

        workspace.Region.Should().Be(Region.Europe);
    }

    [Fact]
    public void SetRegion_ByOwner_UpdatesAndRaisesEvent()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);
        workspace.ClearDomainEvents();

        var result = workspace.SetRegion(Region.Europe, ownerId, At);

        result.IsSuccess.Should().BeTrue();
        workspace.Region.Should().Be(Region.Europe);
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceRegionChanged>();
    }

    [Fact]
    public void SetRegion_ByNonOwner_ReturnsForbiddenFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);

        var result = workspace.SetRegion(Region.Europe, otherUser, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.CannotChangeRegion.Code);
    }

    [Fact]
    public void NewWorkspace_HasRequireTwoFactorFalse()
    {
        var workspace = NewWorkspace();

        workspace.RequireTwoFactor.Should().BeFalse();
    }

    [Fact]
    public void SetRequireTwoFactor_ByOwner_TogglesAndRaisesEvent()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);
        workspace.ClearDomainEvents();

        var result = workspace.SetRequireTwoFactor(true, ownerId, At);

        result.IsSuccess.Should().BeTrue();
        workspace.RequireTwoFactor.Should().BeTrue();
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceTwoFactorRequirementChanged>();
    }

    [Fact]
    public void SetRequireTwoFactor_ByOwnerToFalse_TogglesAndRaisesEvent()
    {
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);
        workspace.SetRequireTwoFactor(true, ownerId, At);
        workspace.ClearDomainEvents();

        var result = workspace.SetRequireTwoFactor(false, ownerId, At);

        result.IsSuccess.Should().BeTrue();
        workspace.RequireTwoFactor.Should().BeFalse();
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceTwoFactorRequirementChanged>();
    }

    [Fact]
    public void SetRequireTwoFactor_ByNonOwner_ReturnsForbiddenFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);

        var result = workspace.SetRequireTwoFactor(true, otherUser, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.InsufficientPermissions.Code);
        workspace.RequireTwoFactor.Should().BeFalse();
    }

    [Fact]
    public void SetRequireTwoFactor_ToSameValue_IsIdempotent()
    {
        // A no-op call must NOT emit a domain event and must NOT
        // bump UpdatedAt. The audit log would otherwise fill up
        // with redundant entries every time a UI re-renders the
        // toggle without actually changing the value.
        var ownerId = Guid.NewGuid();
        var workspace = NewWorkspace(ownerId);
        workspace.SetRequireTwoFactor(false, ownerId, At);
        workspace.ClearDomainEvents();

        var result = workspace.SetRequireTwoFactor(false, ownerId, At);

        result.IsSuccess.Should().BeTrue();
        workspace.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void GuardRegion_WithUnspecifiedDeployment_AllowsAnyRegion()
    {
        var workspace = NewWorkspace();
        workspace.SetRegion(Region.Europe, workspace.OwnerId, At);

        var result = workspace.GuardRegion(Region.Unspecified);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GuardRegion_WithMatchingRegions_Allows()
    {
        var workspace = NewWorkspace();
        workspace.SetRegion(Region.Europe, workspace.OwnerId, At);

        var result = workspace.GuardRegion(Region.Europe);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GuardRegion_WithMismatchedRegions_Fails()
    {
        var ownerId = Guid.NewGuid();
        var workspace = Workspace.Create(
            WorkspaceId.New(),
            WorkspaceName.Create("Acme").Value,
            ownerId,
            Region.Europe,
            At).Value;

        var result = workspace.GuardRegion(Region.NorthAmerica);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.RegionMismatch.Code);
    }

    [Fact]
    public void GuardRegion_WithUnspecifiedWorkspaceRegion_Allows()
    {
        var workspace = NewWorkspace();

        var result = workspace.GuardRegion(Region.Europe);

        result.IsSuccess.Should().BeTrue();
    }

    // BETA-R2-A2-009 — soft-delete the workspace.

    [Fact]
    public void Delete_SetsIsDeletedAndRaisesEvent()
    {
        var workspace = NewWorkspace();
        workspace.ClearDomainEvents();

        workspace.Delete(At);

        workspace.IsDeleted.Should().BeTrue();
        workspace.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkspaceDeleted>();
    }

    [Fact]
    public void Delete_IsIdempotent()
    {
        var workspace = NewWorkspace();
        workspace.Delete(At);
        workspace.Delete(At);
        workspace.Delete(At);

        workspace.IsDeleted.Should().BeTrue();
        workspace.DomainEvents.OfType<WorkspaceDeleted>().Should().HaveCount(1);
    }
}
