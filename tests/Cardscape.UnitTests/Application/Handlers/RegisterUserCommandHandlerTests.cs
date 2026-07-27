using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class RegisterUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidData_PersistsUserAndReturnsAuthResponse()
    {
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("alice@example.com", "Alice", "Passw0rd!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ctx.Users.All.Should().HaveCount(1);
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
        ctx.Tokens.AccessTokensIssued.Should().HaveCount(1);
        ctx.Tokens.RefreshTokensIssued.Should().HaveCount(1);
        result.Value.User.Email.Should().Be("alice@example.com");
        result.Value.User.DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsValidationFailure()
    {
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("not-an-email", "Alice", "Passw0rd!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.email.invalid");
        ctx.Users.All.Should().BeEmpty();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankDisplayName_ReturnsValidationFailure(string displayName)
    {
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("alice@example.com", displayName, "Passw0rd!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.display_name.required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Handle_WithShortPassword_ReturnsPasswordValidationFailure(string password)
    {
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("alice@example.com", "Alice", password);

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.invalid_password");
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsAlreadyTaken()
    {
        var ctx = new HandlersTestContext();
        await ctx.SeedUserAsync(email: "alice@example.com", password: "Passw0rd!");
        var cmd = new RegisterUserCommand("alice@example.com", "Alice2", "Passw0rd!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.email_taken");
        ctx.Users.All.Should().HaveCount(1);  // original user untouched
    }
}
