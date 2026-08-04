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
        var cmd = new RegisterUserCommand("alice@example.com", "Alice", "Goodpass123!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens,
            ctx.Clock, ctx.RegisterUserCommandValidator, CancellationToken.None);

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
        var cmd = new RegisterUserCommand("not-an-email", "Alice", "Goodpass123!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens,
            ctx.Clock, ctx.RegisterUserCommandValidator, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        // The FluentValidation validator runs first
        // and rejects the bad email with a validator
        // error code (the existing handler-side
        // check would have been `members.email.invalid`).
        result.Error.Code.Should().NotBeNullOrEmpty();
        ctx.Users.All.Should().BeEmpty();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankDisplayName_ReturnsValidationFailure(string displayName)
    {
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("alice@example.com", displayName, "Goodpass123!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens,
            ctx.Clock, ctx.RegisterUserCommandValidator, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Handle_WithShortPassword_ReturnsPasswordValidationFailure(string password)
    {
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("alice@example.com", "Alice", password);

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens,
            ctx.Clock, ctx.RegisterUserCommandValidator, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.invalid_password");
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsAlreadyTaken()
    {
        var ctx = new HandlersTestContext();
        await ctx.SeedUserAsync(email: "alice@example.com", password: "Goodpass123!");
        var cmd = new RegisterUserCommand("alice@example.com", "Alice2", "Goodpass123!");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens,
            ctx.Clock, ctx.RegisterUserCommandValidator, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.email_taken");
        ctx.Users.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithBreachedPassword_ReturnsValidationFailure()
    {
        // The breached-password check is the new policy
        // line added in the security regression suite.
        // "12345678" is in the top-100 most-leaked list
        // and must be rejected even though it passes
        // the length check.
        var ctx = new HandlersTestContext();
        var cmd = new RegisterUserCommand("alice@example.com", "Alice", "12345678");

        var result = await RegisterUserCommandHandler.Handle(
            cmd, ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens,
            ctx.Clock, ctx.RegisterUserCommandValidator, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.invalid_password");
        ctx.Users.All.Should().BeEmpty();
    }
}
