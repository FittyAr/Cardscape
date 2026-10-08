using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Boards.Queries;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

/// <summary>
/// Board roster management: board Admins, workspace managers and
/// active instance admins manage members; plain members and observers
/// cannot; anyone can leave; the last board Admin is protected; only
/// workspace members can be added.
/// </summary>
public sealed class BoardMemberHandlerTests
{
    private sealed record Scenario(
        HandlersTestContext Ctx,
        User Owner,
        User BoardAdmin,
        User Member,
        User Outsider,
        Workspace Workspace,
        Board Board);

    /// <summary>
    /// Workspace owned by <c>Owner</c> with BoardAdmin and Member as
    /// workspace Members; the board is created by BoardAdmin (its only
    /// Admin) and Member is a board Member. The owner is not on the board.
    /// </summary>
    private static async Task<Scenario> SeedAsync()
    {
        var ctx = new HandlersTestContext();
        User owner = await ctx.SeedUserAsync("owner@example.com", "Owner");
        User boardAdmin = await ctx.SeedUserAsync("admin@example.com", "Board Admin");
        User member = await ctx.SeedUserAsync("member@example.com", "Member");
        User outsider = await ctx.SeedUserAsync("outsider@example.com", "Outsider");
        Workspace workspace = await ctx.SeedWorkspaceAsync(owner.Id.Value);
        workspace.AddMember(boardAdmin.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        workspace.AddMember(member.Id.Value, WorkspaceRole.Member, ctx.Clock.UtcNow);
        Board board = await ctx.SeedBoardAsync(workspace.Id, boardAdmin.Id.Value);
        board.AddMember(member.Id.Value, BoardMemberRole.Member, ctx.Clock.UtcNow);
        return new Scenario(ctx, owner, boardAdmin, member, outsider, workspace, board);
    }

    private static Task<Result> AddAsync(Scenario s, User actor, Guid userId, BoardMemberRole role)
    {
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(actor);
        return AddBoardMemberCommandHandler.HandleAsync(
            new AddBoardMemberCommand(s.Board.Id.Value, userId, role),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock,
            CancellationToken.None);
    }

    private static Task<Result> ChangeAsync(Scenario s, User actor, Guid userId, BoardMemberRole role)
    {
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(actor);
        return ChangeBoardMemberRoleCommandHandler.HandleAsync(
            new ChangeBoardMemberRoleCommand(s.Board.Id.Value, userId, role),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock,
            CancellationToken.None);
    }

    private static Task<Result> RemoveAsync(Scenario s, User actor, Guid userId)
    {
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(actor);
        return RemoveBoardMemberCommandHandler.HandleAsync(
            new RemoveBoardMemberCommand(s.Board.Id.Value, userId),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.UnitOfWork, s.Ctx.CurrentUser, s.Ctx.Clock,
            CancellationToken.None);
    }

    private static BoardMemberRole? Role(Scenario s, User user) =>
        s.Board.Members.SingleOrDefault(m => m.UserId == user.Id.Value)?.Role;

    [Fact]
    public async Task Add_ByBoardMember_IsForbidden()
    {
        Scenario s = await SeedAsync();
        User colleague = await s.Ctx.SeedUserAsync("colleague@example.com", "Colleague");
        s.Workspace.AddMember(colleague.Id.Value, WorkspaceRole.Member, s.Ctx.Clock.UtcNow);

        var result = await AddAsync(s, s.Member, colleague.Id.Value, BoardMemberRole.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.forbidden");
        s.Board.IsMember(colleague.Id.Value).Should().BeFalse();
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Add_ByBoardAdmin_AddsAWorkspaceMember()
    {
        Scenario s = await SeedAsync();
        User colleague = await s.Ctx.SeedUserAsync("colleague@example.com", "Colleague");
        s.Workspace.AddMember(colleague.Id.Value, WorkspaceRole.Observer, s.Ctx.Clock.UtcNow);

        var result = await AddAsync(s, s.BoardAdmin, colleague.Id.Value, BoardMemberRole.Observer);

        result.IsSuccess.Should().BeTrue();
        Role(s, colleague).Should().Be(BoardMemberRole.Observer);
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Add_ByWorkspaceOwnerNotOnTheBoard_IsAllowed()
    {
        Scenario s = await SeedAsync();

        var result = await AddAsync(s, s.Owner, s.Owner.Id.Value, BoardMemberRole.Admin);

        result.IsSuccess.Should().BeTrue();
        Role(s, s.Owner).Should().Be(BoardMemberRole.Admin);
    }

    [Fact]
    public async Task Add_OfSomeoneOutsideTheWorkspace_IsRefused()
    {
        Scenario s = await SeedAsync();

        var result = await AddAsync(s, s.BoardAdmin, s.Outsider.Id.Value, BoardMemberRole.Member);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.members.not_in_workspace");
        s.Board.IsMember(s.Outsider.Id.Value).Should().BeFalse();
    }

    [Fact]
    public async Task ChangeRole_ByBoardMember_IsForbidden()
    {
        Scenario s = await SeedAsync();

        var result = await ChangeAsync(s, s.Member, s.Member.Id.Value, BoardMemberRole.Admin);

        result.Error.Code.Should().Be("boards.forbidden");
        Role(s, s.Member).Should().Be(BoardMemberRole.Member);
    }

    [Fact]
    public async Task ChangeRole_ByWorkspaceAdmin_IsAllowed()
    {
        Scenario s = await SeedAsync();
        User wsAdmin = await s.Ctx.SeedUserAsync("wsadmin@example.com", "Workspace Admin");
        s.Workspace.AddMember(wsAdmin.Id.Value, WorkspaceRole.Admin, s.Ctx.Clock.UtcNow);

        var result = await ChangeAsync(s, wsAdmin, s.Member.Id.Value, BoardMemberRole.Observer);

        result.IsSuccess.Should().BeTrue();
        Role(s, s.Member).Should().Be(BoardMemberRole.Observer);
    }

    [Fact]
    public async Task ChangeRole_ByActiveInstanceAdmin_IsAllowed()
    {
        Scenario s = await SeedAsync();
        s.Outsider.SetAdmin(true, s.Ctx.Clock.UtcNow);

        var result = await ChangeAsync(s, s.Outsider, s.Member.Id.Value, BoardMemberRole.Admin);

        result.IsSuccess.Should().BeTrue();
        Role(s, s.Member).Should().Be(BoardMemberRole.Admin);
    }

    [Fact]
    public async Task ChangeRole_ByDeactivatedInstanceAdmin_IsForbidden()
    {
        Scenario s = await SeedAsync();
        s.Outsider.SetAdmin(true, s.Ctx.Clock.UtcNow);
        s.Outsider.Deactivate(s.Ctx.Clock.UtcNow);

        var result = await ChangeAsync(s, s.Outsider, s.Member.Id.Value, BoardMemberRole.Admin);

        result.Error.Code.Should().Be("boards.forbidden");
    }

    [Fact]
    public async Task ChangeRole_DemotingTheLastAdmin_IsRefusedEvenForTheWorkspaceOwner()
    {
        Scenario s = await SeedAsync();

        var result = await ChangeAsync(s, s.Owner, s.BoardAdmin.Id.Value, BoardMemberRole.Member);

        result.Error.Code.Should().Be("boards.members.last_admin");
        Role(s, s.BoardAdmin).Should().Be(BoardMemberRole.Admin);
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Remove_ByBoardMemberOfSomeoneElse_IsForbidden()
    {
        Scenario s = await SeedAsync();

        var result = await RemoveAsync(s, s.Member, s.BoardAdmin.Id.Value);

        result.Error.Code.Should().Be("boards.forbidden");
        s.Board.IsMember(s.BoardAdmin.Id.Value).Should().BeTrue();
    }

    [Fact]
    public async Task Remove_Self_LeavesTheBoard()
    {
        Scenario s = await SeedAsync();

        var result = await RemoveAsync(s, s.Member, s.Member.Id.Value);

        result.IsSuccess.Should().BeTrue();
        s.Board.IsMember(s.Member.Id.Value).Should().BeFalse();
        s.Ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Remove_LastAdminLeaving_IsRefused()
    {
        Scenario s = await SeedAsync();

        var result = await RemoveAsync(s, s.BoardAdmin, s.BoardAdmin.Id.Value);

        result.Error.Code.Should().Be("boards.members.last_admin");
        s.Board.IsMember(s.BoardAdmin.Id.Value).Should().BeTrue();
    }

    [Fact]
    public async Task Remove_ByBoardAdmin_RemovesAMember()
    {
        Scenario s = await SeedAsync();

        var result = await RemoveAsync(s, s.BoardAdmin, s.Member.Id.Value);

        result.IsSuccess.Should().BeTrue();
        s.Board.IsMember(s.Member.Id.Value).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_OfNonMember_ReturnsNotFound()
    {
        Scenario s = await SeedAsync();

        var result = await RemoveAsync(s, s.BoardAdmin, s.Outsider.Id.Value);

        result.Error.Code.Should().Be("boards.members.not_found");
    }

    [Fact]
    public async Task List_ReturnsNameEmailAndRole()
    {
        Scenario s = await SeedAsync();
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(s.Member);

        var result = await ListBoardMembersQueryHandler.HandleAsync(
            new ListBoardMembersQuery(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().ContainSingle(m => m.UserId == s.BoardAdmin.Id.Value
            && m.DisplayName == "Board Admin" && m.Email == "admin@example.com" && m.Role == BoardMemberRole.Admin);
    }

    [Fact]
    public async Task List_ByWorkspaceOwnerNotOnTheBoard_IsAllowed_ButNotForOutsiders()
    {
        Scenario s = await SeedAsync();

        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(s.Owner);
        var owner = await ListBoardMembersQueryHandler.HandleAsync(
            new ListBoardMembersQuery(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser, CancellationToken.None);
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(s.Outsider);
        var outsider = await ListBoardMembersQueryHandler.HandleAsync(
            new ListBoardMembersQuery(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser, CancellationToken.None);

        owner.IsSuccess.Should().BeTrue();
        outsider.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Access_ReportsManageRightsAndOwnRole()
    {
        Scenario s = await SeedAsync();

        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(s.Member);
        var member = await GetBoardMemberAccessQueryHandler.HandleAsync(
            new GetBoardMemberAccessQuery(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser, CancellationToken.None);
        s.Ctx.CurrentUser = FakeCurrentUser.AuthenticatedAs(s.Owner);
        var owner = await GetBoardMemberAccessQueryHandler.HandleAsync(
            new GetBoardMemberAccessQuery(s.Board.Id.Value),
            s.Ctx.Boards, s.Ctx.Workspaces, s.Ctx.Users, s.Ctx.CurrentUser, CancellationToken.None);

        member.Value.CanManageMembers.Should().BeFalse();
        member.Value.Role.Should().Be(BoardMemberRole.Member);
        owner.Value.CanManageMembers.Should().BeTrue();
        owner.Value.Role.Should().BeNull();
    }
}
