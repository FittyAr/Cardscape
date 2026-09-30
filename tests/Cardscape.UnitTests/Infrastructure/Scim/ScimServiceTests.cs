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

    [Theory]
    [InlineData("members[value eq \"{0}\"]", true)]
    [InlineData("MEMBERS[VALUE EQ \"{0}\"]", true)]
    [InlineData(" members [ value eq \"{0}\" ] ", true)]
    [InlineData("members[value eq \"{0}\"", false)]
    [InlineData("members[display eq \"{0}\"]", false)]
    [InlineData("members[value ne \"{0}\"]", false)]
    [InlineData("membersOther[value eq \"{0}\"]", false)]
    [InlineData("members[value eq \"{0}\"]suffix", false)]
    public async Task PatchGroupAsync_FilteredMemberRemoval_RemovesOnlyTargetAndPersists(
        string pathFormat, bool removesTarget)
    {
        var context = CreateContext();
        User target = BuildUser("target@example.com", "Target");
        User retained = BuildUser("retained@example.com", "Retained");
        Workspace workspace = BuildWorkspace(context.WorkspaceId, context.User.Id.Value);
        workspace.AddMember(target.Id.Value, WorkspaceRole.Member, Now).IsSuccess.Should().BeTrue();
        workspace.AddMember(retained.Id.Value, WorkspaceRole.Member, Now).IsSuccess.Should().BeTrue();
        context.Workspaces.Setup(x => x.GetByIdAsync(context.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        context.Users.Setup(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([context.User, target, retained]);
        context.UnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        string path = string.Format(System.Globalization.CultureInfo.InvariantCulture, pathFormat, target.Id.Value);

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value,
            $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new ScimPatchOperation("remove", path, null)]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Guid[] expectedMembers = removesTarget
            ? [context.User.Id.Value, retained.Id.Value]
            : [context.User.Id.Value, target.Id.Value, retained.Id.Value];
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo(expectedMembers);
        result.Value.Members.Select(member => member.Value).Should().BeEquivalentTo(
            expectedMembers.Select(id => id.ToString("D")));
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PatchGroupAsync_AddAndReplace_ApplyInOrderAndPreserveOwner(
        bool replaceFirst, bool jsonValues)
    {
        var context = CreateContext();
        User added = BuildUser("added@example.com", "Added");
        User replacement = BuildUser("replacement@example.com", "Replacement");
        User obsolete = BuildUser("obsolete@example.com", "Obsolete");
        Workspace workspace = BuildWorkspace(context.WorkspaceId, context.User.Id.Value);
        workspace.AddMember(obsolete.Id.Value, WorkspaceRole.Member, Now).IsSuccess.Should().BeTrue();
        context.Workspaces.Setup(x => x.GetByIdAsync(context.WorkspaceId, TestContext.Current.CancellationToken))
            .ReturnsAsync(workspace);
        User[] available = [context.User, added, replacement, obsolete];
        context.Users.Setup(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), TestContext.Current.CancellationToken))
            .ReturnsAsync((IReadOnlyList<UserId> ids, CancellationToken _) =>
                available.Where(user => ids.Contains(user.Id)).ToArray());
        context.UnitOfWork.Setup(x => x.SaveChangesAsync(TestContext.Current.CancellationToken)).ReturnsAsync(1);
        object Members(User user)
        {
            ScimGroupMember[] members = [new(user.Id.Value.ToString("D"), null)];
            return jsonValues
                ? System.Text.Json.JsonSerializer.SerializeToElement(members, System.Text.Json.JsonSerializerOptions.Web)
                : members;
        }
        var add = new ScimPatchOperation("add", "members", Members(added));
        var replace = new ScimPatchOperation("replace", "members", Members(replacement));

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest(replaceFirst ? [replace, add] : [add, replace]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Guid[] expected = replaceFirst
            ? [context.User.Id.Value, replacement.Id.Value, added.Id.Value]
            : [context.User.Id.Value, replacement.Id.Value];
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo(expected);
        result.Value.Members.Should().BeEquivalentTo(available.Where(user => expected.Contains(user.Id.Value))
            .Select(user => new ScimGroupMember(user.Id.Value.ToString("D"), user.DisplayName.Value)));
        context.Users.Verify(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), TestContext.Current.CancellationToken), Times.Exactly(3));
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
        context.GlobalUsers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PatchGroupAsync_EmptyReplacement_RetainsOnlyOwner()
    {
        var context = CreateContext();
        User obsolete = BuildUser("obsolete@example.com", "Obsolete");
        Workspace workspace = BuildWorkspace(context.WorkspaceId, context.User.Id.Value);
        workspace.AddMember(obsolete.Id.Value, WorkspaceRole.Member, Now).IsSuccess.Should().BeTrue();
        context.Workspaces.Setup(x => x.GetByIdAsync(context.WorkspaceId, TestContext.Current.CancellationToken)).ReturnsAsync(workspace);
        context.Users.Setup(x => x.ListByIdsAsync(It.Is<IReadOnlyList<UserId>>(ids => ids.Count == 0), TestContext.Current.CancellationToken)).ReturnsAsync([]);
        context.Users.Setup(x => x.ListByIdsAsync(It.Is<IReadOnlyList<UserId>>(ids => ids.Count == 1 && ids.Contains(context.User.Id)), TestContext.Current.CancellationToken)).ReturnsAsync([context.User]);
        context.UnitOfWork.Setup(x => x.SaveChangesAsync(TestContext.Current.CancellationToken)).ReturnsAsync(1);

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new ScimPatchOperation("replace", "members", Array.Empty<ScimGroupMember>())]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workspace.Members.Should().ContainSingle().Which.UserId.Should().Be(context.User.Id.Value);
        result.Value.Members.Should().ContainSingle().Which.Should().Be(new ScimGroupMember(context.User.Id.Value.ToString("D"), "Member User"));
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("members", true)]
    [InlineData("MEMBERS", true)]
    [InlineData("membersOther", false)]
    [InlineData("members.display", false)]
    public async Task PatchGroupAsync_AddMembers_RequiresExactAttribute(string path, bool addsMember)
    {
        var context = CreateContext();
        User incoming = BuildUser("incoming@example.com", "Incoming");
        Workspace workspace = BuildWorkspace(context.WorkspaceId, context.User.Id.Value);
        context.Workspaces.Setup(x => x.GetByIdAsync(context.WorkspaceId, TestContext.Current.CancellationToken)).ReturnsAsync(workspace);
        User[] available = [context.User, incoming];
        context.Users.Setup(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), TestContext.Current.CancellationToken))
            .ReturnsAsync((IReadOnlyList<UserId> ids, CancellationToken _) => available.Where(user => ids.Contains(user.Id)).ToArray());
        context.UnitOfWork.Setup(x => x.SaveChangesAsync(TestContext.Current.CancellationToken)).ReturnsAsync(1);

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new ScimPatchOperation("add", path,
                new ScimGroupMember[] { new(incoming.Id.Value.ToString("D"), null) })]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Guid[] expected = addsMember ? [context.User.Id.Value, incoming.Id.Value] : [context.User.Id.Value];
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo(expected);
        result.Value.Members.Select(member => member.Value).Should().BeEquivalentTo(expected.Select(id => id.ToString("D")));
        context.Users.Verify(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), TestContext.Current.CancellationToken), Times.Exactly(addsMember ? 2 : 1));
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("replace")]
    public async Task PatchGroupAsync_PathlessNameChange_PreservesMembers(string operation)
    {
        var (context, workspace, peer) = CreateGroupContext();
        var value = System.Text.Json.JsonSerializer.SerializeToElement(new { displayName = "Renamed" });

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new(operation, null, value)]), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        workspace.Name.Value.Should().Be("Renamed");
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo([context.User.Id.Value, peer.Id.Value]);
        result.Value.DisplayName.Should().Be("Renamed");
        result.Value.Members.Select(member => member.Value).Should().BeEquivalentTo([context.User.Id.Value.ToString("D"), peer.Id.Value.ToString("D")]);
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("replace")]
    public async Task PatchGroupAsync_PathlessMembers_AppliesOperation(string operation)
    {
        var (context, workspace, peer) = CreateGroupContext();
        User incoming = BuildUser("incoming@example.com", "Incoming");
        User[] available = [context.User, peer, incoming];
        context.Users.Setup(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), TestContext.Current.CancellationToken))
            .ReturnsAsync((IReadOnlyList<UserId> ids, CancellationToken _) => available.Where(user => ids.Contains(user.Id)).ToArray());
        var value = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            displayName = "Combined change",
            members = new[] { new { value = incoming.Id.Value.ToString("D") } }
        });

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new(operation, null, value)]), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        Guid[] expected = operation == "add"
            ? [context.User.Id.Value, peer.Id.Value, incoming.Id.Value]
            : [context.User.Id.Value, incoming.Id.Value];
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo(expected);
        result.Value.Members.Select(member => member.Value).Should().BeEquivalentTo(expected.Select(id => id.ToString("D")));
        workspace.Name.Value.Should().Be("Combined change");
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("42")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    [InlineData("[{\"value\":42}]")]
    [InlineData("[{\"value\":\"not-a-guid\"}]")]
    [InlineData("[{\"value\":\"00000000-0000-0000-0000-000000000000\"}]")]
    [InlineData("[{\"value\":\"11111111-1111-1111-1111-111111111111\"},{}]")]
    public async Task PatchGroupAsync_InvalidMembers_RejectsBeforeAnyMutation(string json)
    {
        var (context, workspace, peer) = CreateGroupContext();
        using var document = System.Text.Json.JsonDocument.Parse(json);

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new("replace", "displayName", "Must not apply"), new("replace", "members", document.RootElement)]),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("scim.invalid_value");
        workspace.Name.Value.Should().Be("SCIM Workspace");
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo([context.User.Id.Value, peer.Id.Value]);
        context.Users.VerifyNoOtherCalls();
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("replace")]
    public async Task PatchGroupAsync_PathlessArray_RejectsWithoutChangingMembers(string operation)
    {
        var (context, workspace, peer) = CreateGroupContext();
        var value = System.Text.Json.JsonSerializer.SerializeToElement(Array.Empty<object>());

        Result<ScimGroup> result = await context.Service.PatchGroupAsync(
            context.WorkspaceId.Value, $"workspace-{context.WorkspaceId.Value:D}",
            new ScimPatchRequest([new(operation, null, value)]), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("scim.invalid_value");
        workspace.Members.Select(member => member.UserId).Should().BeEquivalentTo([context.User.Id.Value, peer.Id.Value]);
        context.Users.VerifyNoOtherCalls();
        context.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ScimTestContext Context, Workspace Workspace, User Peer) CreateGroupContext()
    {
        var context = CreateContext();
        User peer = BuildUser("peer@example.com", "Peer");
        Workspace workspace = BuildWorkspace(context.WorkspaceId, context.User.Id.Value);
        workspace.AddMember(peer.Id.Value, WorkspaceRole.Member, Now).IsSuccess.Should().BeTrue();
        context.Workspaces.Setup(x => x.GetByIdAsync(context.WorkspaceId, TestContext.Current.CancellationToken)).ReturnsAsync(workspace);
        User[] available = [context.User, peer];
        context.Users.Setup(x => x.ListByIdsAsync(It.IsAny<IReadOnlyList<UserId>>(), TestContext.Current.CancellationToken))
            .ReturnsAsync((IReadOnlyList<UserId> ids, CancellationToken _) => available.Where(user => ids.Contains(user.Id)).ToArray());
        context.UnitOfWork.Setup(x => x.SaveChangesAsync(TestContext.Current.CancellationToken)).ReturnsAsync(1);
        return (context, workspace, peer);
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
