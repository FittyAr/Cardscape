using Cardscape.Domain.Audit;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Members.Events;
using Cardscape.Domain.Workspaces.Events;
using Cardscape.Infrastructure.Persistence.Audit;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

public sealed class AuditEventMapperTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<IDomainEvent, string> UserEvents()
    {
        UserId id = UserId.New();
        return new TheoryData<IDomainEvent, string>
        {
            { new UserGrantedAdmin(id, At), AuditActions.UserAdminGranted },
            { new UserRevokedAdmin(id, At), AuditActions.UserAdminRevoked },
            { new UserDeactivated(id, At), AuditActions.UserDeactivated },
            { new UserReactivated(id, At), AuditActions.UserReactivated },
            { new UserSoftDeleted(id, At), AuditActions.UserDeleted },
            { new UserRestored(id, At), AuditActions.UserRestored },
            { new UserAnonymised(id, At), AuditActions.UserAnonymised },
            { new UserRestricted(id, At), AuditActions.UserRestricted },
            { new UserUnrestricted(id, At), AuditActions.UserUnrestricted },
            { new UserPasswordResetByAdmin(id, At), AuditActions.UserPasswordResetByAdmin },
            { new UserCreatedByAdmin(id, At), AuditActions.UserCreatedByAdmin },
        };
    }

    [Theory]
    [MemberData(nameof(UserEvents))]
    public void AccountEvents_TargetTheUser(IDomainEvent domainEvent, string action)
    {
        AuditDraft draft = AuditEventMapper.Map(domainEvent)!;

        draft.Action.Should().Be(action);
        draft.TargetType.Should().Be(AuditTargetTypes.User);
        draft.TargetId.Should().Be(((dynamic)domainEvent).UserId.Value);
        draft.OccurredAt.Should().Be(At);
        draft.FallbackActorId.Should().BeNull("only self-service events name the user as actor");
        AuditActions.All.Should().Contain(action);
    }

    [Fact]
    public void SelfServiceEvents_FallBackToTheUserAsActor()
    {
        UserId id = UserId.New();
        EmailAddress email = EmailAddress.Create("someone@example.com").Value;

        AuditEventMapper.Map(new UserRegistered(id, email, At))!.FallbackActorId.Should().Be(id.Value);
        AuditEventMapper.Map(new UserEmailVerified(id, email, At))!.FallbackActorId.Should().Be(id.Value);
    }

    [Fact]
    public void WorkspaceRoleChange_RecordsThePreviousAndNewRole()
    {
        WorkspaceId workspaceId = WorkspaceId.New();
        Guid userId = Guid.NewGuid();

        AuditDraft draft = AuditEventMapper.Map(
            new WorkspaceMemberRoleChanged(workspaceId, userId, WorkspaceRole.Admin, At),
            new AuditEventFacts(PreviousRole: "Member"))!;

        draft.Action.Should().Be(AuditActions.WorkspaceMemberRoleChanged);
        draft.TargetId.Should().Be(userId);
        draft.WorkspaceId.Should().Be(workspaceId.Value);
        draft.Details.Should().Equal(new Dictionary<string, string> { ["from"] = "Member", ["to"] = "Admin" });
    }

    [Fact]
    public void Invitations_TargetTheInvitation_NamedByItsEmail()
    {
        WorkspaceId workspaceId = WorkspaceId.New();
        WorkspaceInvitationId invitationId = WorkspaceInvitationId.New();
        AuditEventFacts facts = new(InvitationEmail: "guest@example.com", InvitationRole: "Observer");

        AuditDraft issued = AuditEventMapper.Map(
            new WorkspaceInvitationIssued(invitationId, workspaceId, "Guest@Example.com", At), facts)!;
        AuditDraft revoked = AuditEventMapper.Map(new WorkspaceInvitationRevoked(invitationId, workspaceId, At), facts)!;

        foreach (AuditDraft draft in new[] { issued, revoked })
        {
            draft.TargetType.Should().Be(AuditTargetTypes.Invitation);
            draft.TargetId.Should().Be(invitationId.Value);
            draft.TargetName.Should().Be("guest@example.com");
            draft.Details.Should().Contain("role", "Observer");
        }

        issued.Action.Should().Be(AuditActions.WorkspaceInvitationIssued);
        revoked.Action.Should().Be(AuditActions.WorkspaceInvitationRevoked);
    }

    [Fact]
    public void OwnershipTransfer_NamesThePreviousOwner_AndFallsBackToTheEventActor()
    {
        Guid previous = Guid.NewGuid();
        Guid next = Guid.NewGuid();
        Guid actor = Guid.NewGuid();

        AuditDraft draft = AuditEventMapper.Map(
            new WorkspaceOwnershipTransferred(WorkspaceId.New(), previous, next, actor, At))!;

        draft.TargetId.Should().Be(next);
        draft.NamedUsers.Should().Equal(new Dictionary<string, Guid> { ["previousOwner"] = previous });
        draft.FallbackActorId.Should().Be(actor);
    }

    [Fact]
    public void BoardEvents_CarryTheBoardAndRoles()
    {
        BoardId boardId = BoardId.New();
        Guid userId = Guid.NewGuid();

        AuditDraft added = AuditEventMapper.Map(new BoardMemberAdded(boardId, userId, BoardMemberRole.Observer, At))!;
        AuditDraft removed = AuditEventMapper.Map(
            new BoardMemberRemoved(boardId, userId, At), new AuditEventFacts(PreviousRole: "Admin"))!;

        added.Action.Should().Be(AuditActions.BoardMemberAdded);
        added.BoardId.Should().Be(boardId.Value);
        added.Details.Should().Contain("role", "Observer");
        removed.Action.Should().Be(AuditActions.BoardMemberRemoved);
        removed.Details.Should().Contain("role", "Admin");
    }

    [Fact]
    public void UnrelatedEvents_AreNotAudited()
    {
        AuditEventMapper.Map(new UserLoggedIn(UserId.New(), At)).Should().BeNull();
        AuditEventMapper.Map(new BoardStarred(BoardId.New(), Guid.NewGuid(), At)).Should().BeNull();
    }

    [Fact]
    public void Collapse_KeepsOneLinePerAdministratorAction()
    {
        UserId created = UserId.New();
        UserId registered = UserId.New();
        EmailAddress email = EmailAddress.Create("someone@example.com").Value;
        WorkspaceId workspaceId = WorkspaceId.New();
        IDomainEvent[] events =
        [
            // CreateUserByAdmin: register + created + verified + temporary password + admin.
            new UserRegistered(created, email, At),
            new UserCreatedByAdmin(created, At),
            new UserEmailVerified(created, email, At),
            new UserPasswordResetByAdmin(created, At),
            new UserGrantedAdmin(created, At),
            // Sign-up through an invitation link: registered + verified + joined.
            new UserRegistered(registered, email, At),
            new UserEmailVerified(registered, email, At),
            new WorkspaceMemberAdded(workspaceId, registered.Value, WorkspaceRole.Member, At),
            new WorkspaceInvitationAccepted(WorkspaceInvitationId.New(), workspaceId, registered.Value, At),
        ];

        IReadOnlyList<AuditDraft> collapsed = AuditEventMapper.Collapse(
            events.Select(e => AuditEventMapper.Map(e)!).ToList());

        collapsed.Select(d => (d.Action, d.TargetId)).Should().Equal(
            (AuditActions.UserCreatedByAdmin, created.Value),
            (AuditActions.UserAdminGranted, created.Value),
            (AuditActions.UserRegistered, registered.Value),
            (AuditActions.WorkspaceInvitationAccepted, registered.Value));
    }

    [Fact]
    public void Collapse_FoldsTheVerificationAnInvitationLinkProves()
    {
        UserId user = UserId.New();
        AuditDraft verified = AuditEventMapper.Map(
            new UserEmailVerified(user, EmailAddress.Create("someone@example.com").Value, At))!;
        AuditDraft accepted = AuditEventMapper.Map(
            new WorkspaceInvitationAccepted(WorkspaceInvitationId.New(), WorkspaceId.New(), user.Value, At))!;

        AuditEventMapper.Collapse([verified, accepted]).Should().Equal(accepted);
        AuditEventMapper.Collapse([verified]).Should().Equal(verified);
    }

    [Fact]
    public void Collapse_KeepsAStandaloneMemberAddition()
    {
        AuditDraft added = AuditEventMapper.Map(
            new WorkspaceMemberAdded(WorkspaceId.New(), Guid.NewGuid(), WorkspaceRole.Member, At))!;

        AuditEventMapper.Collapse([added]).Should().Equal(added);
    }

    [Fact]
    public void WorkspaceEvents_TargetTheWorkspace()
    {
        WorkspaceId id = WorkspaceId.New();
        WorkspaceName name = WorkspaceName.Create("Nexora").Value;

        AuditDraft created = AuditEventMapper.Map(new WorkspaceCreated(id, Guid.NewGuid(), name, At))!;
        AuditDraft renamed = AuditEventMapper.Map(new WorkspaceRenamed(id, name, At), new AuditEventFacts(PreviousName: "Old"))!;
        AuditDraft deleted = AuditEventMapper.Map(new WorkspaceDeleted(id, At), new AuditEventFacts(WorkspaceName: "Nexora"))!;
        AuditDraft twoFactor = AuditEventMapper.Map(new WorkspaceTwoFactorRequirementChanged(id, true, Guid.NewGuid(), At))!;

        created.Should().BeEquivalentTo(new { Action = AuditActions.WorkspaceCreated, TargetType = AuditTargetTypes.Workspace, TargetId = id.Value, TargetName = "Nexora" });
        renamed.Details.Should().Contain("previousName", "Old");
        deleted.TargetName.Should().Be("Nexora");
        twoFactor.Details.Should().Contain("value", "on");
        twoFactor.FallbackActorId.Should().NotBeNull();
    }

    [Fact]
    public void CreatingAWorkspace_HidesTheOwnersMemberAddedEntry()
    {
        WorkspaceId id = WorkspaceId.New();
        Guid owner = Guid.NewGuid();
        AuditDraft created = AuditEventMapper.Map(new WorkspaceCreated(id, owner, WorkspaceName.Create("Nexora").Value, At))!;
        AuditDraft added = AuditEventMapper.Map(new WorkspaceMemberAdded(id, owner, WorkspaceRole.Admin, At))!;

        AuditEventMapper.Collapse([created, added]).Should().Equal(created);
    }
}
