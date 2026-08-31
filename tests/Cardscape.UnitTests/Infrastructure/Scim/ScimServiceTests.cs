using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Scim;
using Cardscape.Tests.Common.Fakes;
using Moq;

namespace Cardscape.UnitTests.Infrastructure.Scim;

public sealed class ScimServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetUserAsync_UserOutsideTokenWorkspace_ReturnsNotFoundWithoutGlobalLookup()
    {
        var context = CreateContext();

        Result<ScimUserResponse> result = await context.Service.GetUserAsync(
            context.WorkspaceId.Value,
            context.User.Id.Value,
            TestContext.Current.CancellationToken);

        AssertNotFound(result);
        context.Users.Verify(x => x.FindWorkspaceUserAsync(
            context.WorkspaceId,
            context.User.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        context.GlobalUsers.VerifyNoOtherCalls();
        context.UnitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReplaceUserAsync_UserOutsideTokenWorkspace_ReturnsNotFoundWithoutMutationOrPersistence()
    {
        var context = CreateContext();
        string originalDisplayName = context.User.DisplayName.Value;
        DateTimeOffset? originalUpdatedAt = context.User.UpdatedAt;

        Result<ScimUserResponse> result = await context.Service.ReplaceUserAsync(
            context.WorkspaceId.Value,
            context.User.Id.Value,
            new ScimUserCreateRequest("foreign@example.com", "Changed", "Name", true, null),
            TestContext.Current.CancellationToken);

        AssertNotFound(result);
        context.User.DisplayName.Value.Should().Be(originalDisplayName);
        context.User.UpdatedAt.Should().Be(originalUpdatedAt);
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        context.GlobalUsers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PatchUserAsync_UserOutsideTokenWorkspace_ReturnsNotFoundWithoutMutationOrPersistence()
    {
        var context = CreateContext();

        Result<ScimUserResponse> result = await context.Service.PatchUserAsync(
            context.WorkspaceId.Value,
            context.User.Id.Value,
            new ScimPatchRequest([new ScimPatchOperation("replace", "active", false)]),
            TestContext.Current.CancellationToken);

        AssertNotFound(result);
        context.User.IsActive.Should().BeTrue();
        context.User.UpdatedAt.Should().BeNull();
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        context.GlobalUsers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteUserAsync_UserOutsideTokenWorkspace_ReturnsNotFoundWithoutMutationOrPersistence()
    {
        var context = CreateContext();

        Result result = await context.Service.DeleteUserAsync(
            context.WorkspaceId.Value,
            context.User.Id.Value,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("scim.user_not_found");
        context.User.IsActive.Should().BeTrue();
        context.User.UpdatedAt.Should().BeNull();
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        context.GlobalUsers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListUsersAsync_FilteredPage_DelegatesOneNormalizedBoundedQuery()
    {
        var context = CreateContext();
        context.Users.Setup(x => x.ListWorkspaceUsersAsync(
                context.WorkspaceId,
                "member@example.com",
                6,
                200,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([context.User]);

        Result<IReadOnlyList<ScimUserResponse>> result = await context.Service.ListUsersAsync(
            context.WorkspaceId.Value,
            7,
            999,
            "userName eq \"Member@Example.COM\"",
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = context.User.Id.Value,
            UserName = "member@example.com",
            Active = true
        });
        context.Users.Verify(x => x.ListWorkspaceUsersAsync(
            context.WorkspaceId,
            "member@example.com",
            6,
            200,
            It.IsAny<CancellationToken>()), Times.Once);
        context.Users.Verify(x => x.ListByIdsAsync(
            It.IsAny<IReadOnlyList<UserId>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        context.GlobalUsers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListGroupsAsync_WorkspaceWithMembers_BatchesDisplayNameLookupOnce()
    {
        var context = CreateContext();
        User secondUser = BuildUser("second@example.com", "Second Member");
        Workspace workspace = BuildWorkspace(context.WorkspaceId, context.User.Id.Value);
        workspace.AddMember(secondUser.Id.Value, WorkspaceRole.Member, Now).IsSuccess.Should().BeTrue();
        context.Workspaces.Setup(x => x.GetByIdAsync(
                context.WorkspaceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        context.Users.Setup(x => x.ListByIdsAsync(
                It.Is<IReadOnlyList<UserId>>(ids =>
                    ids.Count == 2
                    && ids.Contains(context.User.Id)
                    && ids.Contains(secondUser.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([context.User, secondUser]);

        ScimListResponse<ScimGroup> result = await context.Service.ListGroupsAsync(
            context.WorkspaceId.Value,
            1,
            50,
            TestContext.Current.CancellationToken);

        ScimGroup group = result.Resources.Should().ContainSingle().Which;
        group.Members.Should().BeEquivalentTo(
            [
                new ScimGroupMember(context.User.Id.Value.ToString("D"), "Member User"),
                new ScimGroupMember(secondUser.Id.Value.ToString("D"), "Second Member")
            ]);
        context.Users.Verify(x => x.ListByIdsAsync(
            It.IsAny<IReadOnlyList<UserId>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        context.GlobalUsers.Verify(x => x.GetByIdAsync(
            It.IsAny<UserId>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUserAsync_UserInsideTokenWorkspace_ReturnsExactUser()
    {
        var context = CreateContext(userBelongsToWorkspace: true);

        Result<ScimUserResponse> result = await context.Service.GetUserAsync(
            context.WorkspaceId.Value,
            context.User.Id.Value,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            Id = context.User.Id.Value,
            UserName = "member@example.com",
            GivenName = "Member",
            FamilyName = "User",
            Active = true,
            CreatedAt = Now.AddDays(-1),
            LastModifiedAt = (DateTimeOffset?)null
        });
        context.UnitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PatchUserAsync_UserInsideTokenWorkspace_DeactivatesAndPersistsOnce()
    {
        var context = CreateContext(userBelongsToWorkspace: true);
        context.UnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        Result<ScimUserResponse> result = await context.Service.PatchUserAsync(
            context.WorkspaceId.Value,
            context.User.Id.Value,
            new ScimPatchRequest([new ScimPatchOperation("replace", "active", false)]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Active.Should().BeFalse();
        result.Value.LastModifiedAt.Should().Be(Now);
        context.User.IsActive.Should().BeFalse();
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ScimTestContext CreateContext(bool userBelongsToWorkspace = false)
    {
        WorkspaceId workspaceId = WorkspaceId.New();
        User user = BuildUser("member@example.com", "Member User");
        var globalUsers = new Mock<IRepository<User, UserId>>(MockBehavior.Strict);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var workspaces = new Mock<IRepository<Workspace, WorkspaceId>>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);

        users.Setup(x => x.FindWorkspaceUserAsync(
                workspaceId,
                user.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(userBelongsToWorkspace ? user : null);

        var service = new ScimService(
            globalUsers.Object,
            users.Object,
            workspaces.Object,
            unitOfWork.Object,
            new FakeClock(Now));
        return new ScimTestContext(
            workspaceId, user, service, globalUsers, users, workspaces, unitOfWork);
    }

    private static User BuildUser(string email, string displayName) => User.RegisterExternal(
        UserId.New(),
        EmailAddress.Create(email).Value,
        DisplayName.Create(displayName).Value,
        Now.AddDays(-1)).Value;

    private static Workspace BuildWorkspace(WorkspaceId id, Guid ownerId) => Workspace.Create(
        id,
        WorkspaceName.Create("SCIM Workspace").Value,
        ownerId,
        Region.Unspecified,
        Now.AddDays(-1)).Value;

    private static void AssertNotFound(Result<ScimUserResponse> result)
    {
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("scim.user_not_found");
    }

    private sealed record ScimTestContext(
        WorkspaceId WorkspaceId,
        User User,
        ScimService Service,
        Mock<IRepository<User, UserId>> GlobalUsers,
        Mock<IUserRepository> Users,
        Mock<IRepository<Workspace, WorkspaceId>> Workspaces,
        Mock<IUnitOfWork> UnitOfWork);
}
