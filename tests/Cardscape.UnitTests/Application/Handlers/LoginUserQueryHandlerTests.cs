using Cardscape.Application.Authentication.Queries;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Tests.Common.Fakes;
using OtpNet;

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
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Email.Should().Be("alice@example.com");
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RequiresTotp.Should().BeFalse();
        result.Value.PendingTotpToken.Should().BeNull();
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
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

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
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

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
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.user.inactive");
    }

    [Fact]
    public async Task Handle_WithTotpEnrolledAndNoCode_ReturnsChallengeWithoutJwt()
    {
        // Arrange — seed a user and enrol them in 2FA.
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!");
        await ctx.SeedTotpCredentialAsync(user);

        // Act
        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "Passw0rd!"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

        // Assert — the password check still passes (no failure
        // path), but the JWT is NOT issued: the response is a
        // challenge that the Web UI has to follow up on.
        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresTotp.Should().BeTrue();
        result.Value.AccessToken.Should().BeNull();
        result.Value.PendingTotpToken.Should().NotBeNullOrWhiteSpace();
        result.Value.User.Email.Should().Be("alice@example.com");
        ctx.Tokens.AccessTokensIssued.Should().BeEmpty();
        user.LastLoginAt.Should().BeNull();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithTotpEnrolledAndValidCode_ReturnsFullAuthResponse()
    {
        // Arrange — seed a user, enrol them in 2FA, then ask
        // OtpNet to compute the current code for the secret the
        // IdentitySecretProtector stored verbatim.
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!");
        var (_, secret) = await ctx.SeedTotpCredentialAsync(user);
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        string code = totp.ComputeTotp();

        // Act
        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "Passw0rd!", code),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

        // Assert — full JWT, the LastUsedCounter advanced.
        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresTotp.Should().BeFalse();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        ctx.Tokens.AccessTokensIssued.Should().HaveCount(1);
        var stored = await ctx.TotpCredentials.FindForUserAsync(user.Id, TestContext.Current.CancellationToken);
        stored!.LastUsedCounter.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithTotpEnrolledAndInvalidCode_ReturnsInvalidCode()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!");
        await ctx.SeedTotpCredentialAsync(user);

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "Passw0rd!", "000000"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins, CancellationToken.None);

        // The TOTP verifier rejects "000000" because the time
        // window it maps to does not match the secret the user
        // enrolled. The handler should propagate the
        // auth.totp.invalid_code error and NOT issue a JWT.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.totp.invalid_code");
        ctx.Tokens.AccessTokensIssued.Should().BeEmpty();
        user.LastLoginAt.Should().BeNull();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenWorkspaceRequiresTotpAndUserIsNotEnrolled_DeniesJwt()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("alice@example.com", "Alice", "Passw0rd!");
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        workspace.SetRequireTwoFactor(true, user.Id.Value, ctx.Clock.UtcNow);

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("alice@example.com", "Passw0rd!"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.totp.enrollment_required");
        ctx.Tokens.AccessTokensIssued.Should().BeEmpty();
        user.LastLoginAt.Should().BeNull();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenWorkspaceRequiresTotpAndEnrollmentIsPending_DeniesJwt()
    {
        var ctx = new HandlersTestContext();
        var user = await ctx.SeedUserAsync("pending@example.com", "Pending", "Passw0rd!");
        var workspace = await ctx.SeedWorkspaceAsync(user.Id.Value);
        workspace.SetRequireTwoFactor(true, user.Id.Value, ctx.Clock.UtcNow);
        await ctx.SeedTotpCredentialAsync(user, confirmed: false);

        var result = await LoginUserQueryHandler.Handle(
            new LoginUserQuery("pending@example.com", "Passw0rd!"),
            ctx.Users, ctx.PasswordHasher, ctx.UnitOfWork, ctx.Tokens, ctx.Clock,
            ctx.TotpCredentials, ctx.Workspaces, ctx.TotpService, ctx.PendingTotpLogins,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("auth.totp.enrollment_required");
        ctx.Tokens.AccessTokensIssued.Should().BeEmpty();
        user.LastLoginAt.Should().BeNull();
        ctx.UnitOfWork.SaveChangesCallCount.Should().Be(0);
    }
}
