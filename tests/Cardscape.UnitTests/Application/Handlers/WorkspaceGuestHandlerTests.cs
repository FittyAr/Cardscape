using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Boards.Queries;
using Cardscape.Application.Common;
using Cardscape.Application.Search;
using Cardscape.Application.Workspaces.Commands;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using Moq;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// Workspace guests only reach the boards they were explicitly added
/// to: listings, search and board reads hide everything else, they
/// cannot create boards or see the full roster, their board role is
/// capped at Member, and turning a member into a guest demotes their
/// board Admin roles.
/// </summary>
public sealed class WorkspaceGuestHandlerTests
{
    private sealed record Scenario(
        HandlersTestContext Ctx,
        User Owner,
        User Member,
        User Guest,
        Workspace Workspace,
        Board SharedBoard,
        Board WorkspaceBoard,
        Board PrivateBoard);

    /// <summary>
    /// Owner + full Member + Guest. The guest is a board Member of
    /// <c>SharedBoard</c> only; <c>WorkspaceBoard</c> is
    /// workspace-visible and <c>PrivateBoard</c> private, both created
    /// by the owner with the member on the private one.
    /// </summary>
    private static async Task<Scenario> SeedAsync()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        User guest = await ctx.SeedUserAsync("guest@example.com", "Guest");
        Workspace workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        workspace.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        workspace.AddMember(guest.Id.Value, WorkspaceRole.Guest, ctx.Clock.UtcNow);

        Board shared = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value, "Shared");
        shared.AddMember(guest.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);
        Board workspaceVisible = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value, "Everyone");
        workspaceVisible.ChangeVisibility(BoardVisibility.Workspace, ctx.Clock.UtcNow);
        Board privateBoard = await ctx.SeedBoardAsync(workspace.Id, owner.Id.Value, "Secret");
        privateBoard.AddMember(member.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);

        return new Scenario(ctx, owner, member, guest, workspace, shared, workspaceVisible, privateBoard);
    }

    private static void ActAs(Scenario s, User user) => s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(user);

    [Fact]
    public async Task ListBoards_ForAGuest_ShowsOnlyTheirBoards()
    {
        Scenario s = await SeedAsync();

        ActAs(s, s.Guest);
        var guest = await ListBoardsForWorkspaceQueryHandler.HandleAsync(
            new ListBoardsForWorkspaceQuery(s.Workspace.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);
        ActAs(s, s.Member);
        var member = await ListBoardsForWorkspaceQueryHandler.HandleAsync(
            new ListBoardsForWorkspaceQuery(s.Workspace.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);

        guest.Value.Select(b => b.Id).Should().BeEquivalentTo([s.SharedBoard.Id.Value]);
        member.Value.Should().HaveCount(3, "full members keep the workspace-wide listing");
    }

    [Fact]
    public async Task GetBoard_WorkspaceVisible_IsRefusedToGuests_ButOpenToFullMembers()
    {
        Scenario s = await SeedAsync();

        ActAs(s, s.Guest);
        var guest = await GetBoardQueryHandler.HandleAsync(
            new GetBoardQuery(s.WorkspaceBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);
        var shared = await GetBoardQueryHandler.HandleAsync(
            new GetBoardQuery(s.SharedBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);
        ActAs(s, s.Member);
        var member = await GetBoardQueryHandler.HandleAsync(
            new GetBoardQuery(s.WorkspaceBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);

        guest.Error.Code.Should().Be("boards.not_member");
        shared.IsSuccess.Should().BeTrue();
        member.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetBoard_Public_StaysReadableForGuests()
    {
        Scenario s = await SeedAsync();
        s.WorkspaceBoard.ChangeVisibility(BoardVisibility.Public, s.Ctx.Clock.UtcNow);

        ActAs(s, s.Guest);
        var result = await GetBoardQueryHandler.HandleAsync(
            new GetBoardQuery(s.WorkspaceBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBoard_ByAGuest_IsRefused()
    {
        Scenario s = await SeedAsync();
        ActAs(s, s.Guest);

        var result = await CreateBoardCommandHandler.HandleAsync(
            new CreateBoardCommand(s.Workspace.Id.Value, "Mine", null, BoardVisibility.Private),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Settings, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, s.Ctx.Activities,
            CancellationToken.None);

        result.Error.Code.Should().Be("workspaces.guest_forbidden");
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Search_ForAGuest_IsScopedToTheirBoards()
    {
        Scenario s = await SeedAsync();
        IReadOnlySet<Guid>? allowed = null;
        var search = new Mock<ISearchService>();
        search.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<SearchHitKind?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlySet<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, SearchHitKind?, int, int, IReadOnlySet<Guid>, CancellationToken>(
                (_, _, _, _, _, ids, _) => allowed = ids)
            .ReturnsAsync(new SearchPage([], 0));
        ActAs(s, s.Guest);

        var result = await SearchQueryHandler.HandleAsync(
            new SearchQuery("anything"), search.Object, s.Ctx.CurrentUser, s.Ctx.Boards, s.Ctx.Workspaces, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        allowed.Should().BeEquivalentTo([s.SharedBoard.Id.Value]);
    }

    [Fact]
    public async Task VisibleBoardIds_KeepTheWorkspaceWideScopeForFullMembers()
    {
        Scenario s = await SeedAsync();

        HashSet<Guid> member = await WorkspaceBoardScope.CollectVisibleBoardIdsAsync(
            s.Ctx.Boards, s.Ctx.Workspaces, s.Member.Id.Value, CancellationToken.None);
        HashSet<Guid> guest = await WorkspaceBoardScope.CollectVisibleBoardIdsAsync(
            s.Ctx.Boards, s.Ctx.Workspaces, s.Guest.Id.Value, CancellationToken.None);

        member.Should().HaveCount(3);
        guest.Should().BeEquivalentTo([s.SharedBoard.Id.Value]);
    }

    [Fact]
    public async Task StarredBoards_HideBoardsAGuestWasRemovedFrom()
    {
        Scenario s = await SeedAsync();
        s.SharedBoard.Star(s.Guest.Id.Value, s.Ctx.Clock.UtcNow);
        s.WorkspaceBoard.Star(s.Guest.Id.Value, s.Ctx.Clock.UtcNow);
        ActAs(s, s.Guest);

        var result = await ListStarredBoardsQueryHandler.HandleAsync(
            new ListStarredBoardsQuery(), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.CurrentUser, CancellationToken.None);

        result.Value.Select(b => b.Id).Should().BeEquivalentTo([s.SharedBoard.Id.Value]);
    }

    [Fact]
    public async Task WorkspaceRoster_ForAGuest_OnlyListsPeopleOnSharedBoards()
    {
        Scenario s = await SeedAsync();

        ActAs(s, s.Guest);
        var guest = await ListWorkspaceMembersQueryHandler.HandleAsync(
            new ListWorkspaceMembersQuery(s.Workspace.Id.Value), s.Ctx.Workspaces, s.Ctx.Boards, s.Ctx.Users, s.Ctx.CurrentUser,
            CancellationToken.None);
        ActAs(s, s.Member);
        var member = await ListWorkspaceMembersQueryHandler.HandleAsync(
            new ListWorkspaceMembersQuery(s.Workspace.Id.Value), s.Ctx.Workspaces, s.Ctx.Boards, s.Ctx.Users, s.Ctx.CurrentUser,
            CancellationToken.None);

        // The owner shares "Shared" with the guest; the plain member does not.
        guest.Value.Select(m => m.UserId).Should().BeEquivalentTo([s.Owner.Id.Value, s.Guest.Id.Value]);
        member.Value.Should().HaveCount(3);
        member.Value.Single(m => m.UserId == s.Guest.Id.Value).Role.Should().Be(WorkspaceRole.Guest);
    }

    [Fact]
    public async Task AddBoardMember_AGuestAsAdmin_IsRefused_AsMember_IsAllowed()
    {
        Scenario s = await SeedAsync();
        ActAs(s, s.Owner);

        Result admin = await AddBoardMemberCommandHandler.HandleAsync(
            new AddBoardMemberCommand(s.PrivateBoard.Id.Value, s.Guest.Id.Value, BoardMemberRole.Admin),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);
        Result member = await AddBoardMemberCommandHandler.HandleAsync(
            new AddBoardMemberCommand(s.PrivateBoard.Id.Value, s.Guest.Id.Value, BoardMemberRole.Member),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);

        admin.Error.Code.Should().Be("boards.members.guest_cannot_be_admin");
        member.IsSuccess.Should().BeTrue();
        s.PrivateBoard.Members.Single(m => m.UserId == s.Guest.Id.Value).Role.Should().Be(BoardMemberRole.Member);
    }

    [Fact]
    public async Task ChangeBoardMemberRole_PromotingAGuestToAdmin_IsRefused()
    {
        Scenario s = await SeedAsync();
        ActAs(s, s.Owner);

        Result result = await ChangeBoardMemberRoleCommandHandler.HandleAsync(
            new ChangeBoardMemberRoleCommand(s.SharedBoard.Id.Value, s.Guest.Id.Value, BoardMemberRole.Admin),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);

        result.Error.Code.Should().Be("boards.members.guest_cannot_be_admin");
        s.SharedBoard.Members.Single(m => m.UserId == s.Guest.Id.Value).Role.Should().Be(BoardMemberRole.Member);
    }

    [Fact]
    public async Task BoardMemberAccess_LetsWorkspaceManagersInviteGuests()
    {
        Scenario s = await SeedAsync();
        s.SharedBoard.AddMember(s.Member.Id.Value, BoardMemberRole.Admin, s.Ctx.Clock.UtcNow);

        ActAs(s, s.Owner);
        var owner = await GetBoardMemberAccessQueryHandler.HandleAsync(
            new GetBoardMemberAccessQuery(s.SharedBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser,
            CancellationToken.None);
        ActAs(s, s.Member);
        var boardAdmin = await GetBoardMemberAccessQueryHandler.HandleAsync(
            new GetBoardMemberAccessQuery(s.SharedBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser,
            CancellationToken.None);
        var roster = await ListBoardMembersQueryHandler.HandleAsync(
            new ListBoardMembersQuery(s.SharedBoard.Id.Value), s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser,
            CancellationToken.None);

        owner.Value.CanInviteGuests.Should().BeTrue();
        boardAdmin.Value.CanManageMembers.Should().BeTrue();
        boardAdmin.Value.CanInviteGuests.Should().BeFalse("only workspace managers issue workspace invitations");
        roster.Value.Single(m => m.UserId == s.Guest.Id.Value).IsWorkspaceGuest.Should().BeTrue();
        roster.Value.Single(m => m.UserId == s.Owner.Id.Value).IsWorkspaceGuest.Should().BeFalse();
    }

    [Fact]
    public async Task MakingAMemberAGuest_KeepsTheirBoards_AndCapsBoardAdminAtMember()
    {
        Scenario s = await SeedAsync();
        // The member co-administers "Secret" with the owner.
        s.PrivateBoard.ChangeMemberRole(s.Member.Id.Value, BoardMemberRole.Admin, s.Ctx.Clock.UtcNow);
        ActAs(s, s.Owner);

        Result<WorkspaceDto> result = await ChangeWorkspaceMemberRoleCommandHandler.HandleAsync(
            new ChangeWorkspaceMemberRoleCommand(s.Workspace.Id.Value, s.Member.Id.Value, WorkspaceRole.Guest),
            s.Ctx.Workspaces, s.Ctx.Boards, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        s.Workspace.IsGuest(s.Member.Id.Value).Should().BeTrue();
        s.PrivateBoard.Members.Single(m => m.UserId == s.Member.Id.Value).Role.Should().Be(BoardMemberRole.Member);
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task MakingTheOnlyBoardAdminAGuest_IsRefused_AndNothingChanges()
    {
        Scenario s = await SeedAsync();
        Board own = await s.Ctx.SeedBoardAsync(s.Workspace.Id, s.Member.Id.Value, "Member's own");
        ActAs(s, s.Owner);

        Result<WorkspaceDto> result = await ChangeWorkspaceMemberRoleCommandHandler.HandleAsync(
            new ChangeWorkspaceMemberRoleCommand(s.Workspace.Id.Value, s.Member.Id.Value, WorkspaceRole.Guest),
            s.Ctx.Workspaces, s.Ctx.Boards, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);

        result.Error.Code.Should().Be("workspaces.guest_last_board_admin");
        result.Error.Message.Should().Contain("Member's own");
        s.Workspace.RoleOf(s.Member.Id.Value).Should().Be(WorkspaceRole.Member);
        own.IsAdmin(s.Member.Id.Value).Should().BeTrue();
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AGuest_CanBePromotedBackToAFullRole()
    {
        Scenario s = await SeedAsync();
        ActAs(s, s.Owner);

        Result<WorkspaceDto> result = await ChangeWorkspaceMemberRoleCommandHandler.HandleAsync(
            new ChangeWorkspaceMemberRoleCommand(s.Workspace.Id.Value, s.Guest.Id.Value, WorkspaceRole.Observer),
            s.Ctx.Workspaces, s.Ctx.Boards, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        s.Workspace.HasFullMembership(s.Guest.Id.Value).Should().BeTrue();
    }

    [Fact]
    public async Task AGuest_CannotChangeRoles()
    {
        Scenario s = await SeedAsync();
        ActAs(s, s.Guest);

        Result<WorkspaceDto> result = await ChangeWorkspaceMemberRoleCommandHandler.HandleAsync(
            new ChangeWorkspaceMemberRoleCommand(s.Workspace.Id.Value, s.Member.Id.Value, WorkspaceRole.Guest),
            s.Ctx.Workspaces, s.Ctx.Boards, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock, CancellationToken.None);

        result.Error.Code.Should().Be("workspaces.forbidden");
    }

    [Fact]
    public async Task WorkspaceDto_ReportsTheCallersRole()
    {
        Scenario s = await SeedAsync();
        ActAs(s, s.Guest);

        var result = await GetWorkspaceQueryHandler.HandleAsync(
            new GetWorkspaceQuery(s.Workspace.Id.Value), s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser, CancellationToken.None);

        result.Value.CallerRole.Should().Be(WorkspaceRole.Guest);
    }
}
