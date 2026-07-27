using Cardscape.Application.Authentication.Queries;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class LoginUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsAuthResponseAndUpdatesLastLogin()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!");

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "Passw0rd!"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Email.Should().Be("alice@example.com");
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(1);
        ctx.Tokens.AccessTokensIssued.Should().HaveCount(1);
        user.LastLoginAt.Should().Be(ctx.Clock.UtcNow);
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsInvalidCredentials()
    {
        var ctx = new HandlersTestContext();

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("ghost@example.com", "Passw0rd!"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.invalid_credentials");
        ctx.Tokens.AccessTokensIssued.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsInvalidCredentials()
    {
        var ctx = new HandlersTestContext();
        await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!");

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "wrong-pass"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.invalid_credentials");
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ReturnsInactiveError()
    {
        var ctx = new HandlersTestContext();
        await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!", active: false);

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "Passw0rd!"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.inactive");
    }
}
