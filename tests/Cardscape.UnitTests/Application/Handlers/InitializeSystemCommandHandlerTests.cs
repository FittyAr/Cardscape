using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Setup.Commands;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using Moq;

namespace Cardscape.UnitTests.Application.Handlers;

public class InitializeSystemCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUsersAlreadyExist_ReturnsConflict()
    {
        var ctx = new HandlersTestContext();
        await ctx.SeedUserAsync(email: "existing@example.com", password: "Password123!");

        var mockSettings = new Mock<ISystemSettingsService>();
        var command = new InitializeSystemCommand(
            "Admin User", "admin@cardscape.test", "AdminPassword123!", "Cardscape", "Default");

        var result = await InitializeSystemCommandHandler.HandleAsync(
            command,
            ctx.Users,
            ctx.Workspaces,
            ctx.PasswordHasher,
            ctx.UnitOfWork,
            ctx.Tokens,
            ctx.Clock,
            mockSettings.Object,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Setup.AlreadyInitialized");
        ctx.Users.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OnCleanDatabase_CreatesAdminUserAndDefaultWorkspace()
    {
        var ctx = new HandlersTestContext();
        var mockSettings = new Mock<ISystemSettingsService>();
        mockSettings.Setup(s => s.UpdateSettingsAsync(It.IsAny<UpdateSystemSettingsRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemSettingsDto(InstanceTitle: "Cardscape Test", AllowPublicRegistration: true, DefaultLanguage: "es", JwtAccessTokenMinutes: 60, DatabaseProvider: "Sqlite", Environment: "Test", StorageRoot: "Storage", AppVersion: "1.2.0"));

        var command = new InitializeSystemCommand(
            "Admin User", "admin@cardscape.test", "AdminPassword123!", "Cardscape Test", "Equipo Alpha");

        var result = await InitializeSystemCommandHandler.HandleAsync(
            command,
            ctx.Users,
            ctx.Workspaces,
            ctx.PasswordHasher,
            ctx.UnitOfWork,
            ctx.Tokens,
            ctx.Clock,
            mockSettings.Object,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ctx.Users.All.Should().HaveCount(1);
        User createdUser = ctx.Users.All.First();
        createdUser.IsAdmin.Should().BeTrue();
        createdUser.Email.Value.Should().Be("admin@cardscape.test");
        createdUser.DisplayName.Value.Should().Be("Admin User");

        ctx.Workspaces.All.Should().HaveCount(1);
        ctx.Workspaces.All.First().Name.Value.Should().Be("Equipo Alpha");
        ctx.Workspaces.All.First().OwnerId.Should().Be(createdUser.Id.Value);

        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.User.Email.Should().Be("admin@cardscape.test");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Handle_WithInvalidPassword_ReturnsValidationFailure(string password)
    {
        var ctx = new HandlersTestContext();
        var mockSettings = new Mock<ISystemSettingsService>();

        var command = new InitializeSystemCommand(
            "Admin User", "admin@cardscape.test", password, "Cardscape", "Default");

        var result = await InitializeSystemCommandHandler.HandleAsync(
            command,
            ctx.Users,
            ctx.Workspaces,
            ctx.PasswordHasher,
            ctx.UnitOfWork,
            ctx.Tokens,
            ctx.Clock,
            mockSettings.Object,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.invalid_password");
        ctx.Users.All.Should().BeEmpty();
    }
}
