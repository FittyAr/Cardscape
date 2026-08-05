using System.Security.Claims;
using Cardscape.Api.Authentication;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cardscape.UnitTests.Security;

/// <summary>
/// Behavioural tests for
/// <see cref="AdminOnlyAuthorizationHandler"/> across the two
/// operator postures documented in
/// <c>docs/operations/06-configurable-subsystems.md#admin-authorization</c>:
///
/// <list type="bullet">
///   <item><c>CacheAdminClaim = true</c> (default): trust the
///         claim, only fall back to the DB when the claim is
///         missing.</item>
///   <item><c>CacheAdminClaim = false</c>: never trust the
///         claim, always read <c>users.IsAdmin</c> from the
///         database.</item>
/// </list>
///
/// The two paths are security-relevant: getting the wrong
/// default can either cost a DB hit per admin request or — much
/// worse — let a revoked admin keep its privileges until token
/// expiry. These tests pin the contract so a future refactor
/// cannot silently flip a default.
/// </summary>
public class AdminOnlyAuthorizationHandlerTests
{
    private const string IsAdminClaim = "is_admin";

    [Fact]
    public async Task CacheEnabled_ClaimTrue_SucceedsWithoutDbLookup()
    {
        var users = new InMemoryUserRepository();
        Guid userId = Guid.NewGuid();
        await users.AddAsync(BuildUser(userId, isAdmin: false), TestContext.Current.CancellationToken);
        var handler = BuildHandler(users, cacheEnabled: true);

        AuthorizationHandlerContext context = BuildContext(
            userId, claimValue: "true", otherClaims: new Dictionary<string, string>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "the claim says 'true' and the cached path is enabled; " +
            "the handler must succeed without consulting the DB " +
            "(the row's IsAdmin=false must be ignored).");
    }

    [Fact]
    public async Task CacheEnabled_ClaimFalse_DoesNotSucceed()
    {
        var users = new InMemoryUserRepository();
        Guid userId = Guid.NewGuid();
        // Underneath the user IS an admin, but the claim says
        // they aren't. With the cache enabled the handler
        // trusts the claim.
        await users.AddAsync(BuildUser(userId, isAdmin: true), TestContext.Current.CancellationToken);
        var handler = BuildHandler(users, cacheEnabled: true);

        AuthorizationHandlerContext context = BuildContext(
            userId, claimValue: "false", otherClaims: new Dictionary<string, string>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "the claim says 'false' and the cached path is enabled; " +
            "the user must not be granted admin even if the DB " +
            "row says otherwise.");
    }

    [Fact]
    public async Task CacheEnabled_ClaimMissing_FallsBackToDbAndHonoursRow()
    {
        var users = new InMemoryUserRepository();
        Guid userId = Guid.NewGuid();
        await users.AddAsync(BuildUser(userId, isAdmin: true), TestContext.Current.CancellationToken);
        var handler = BuildHandler(users, cacheEnabled: true);

        // No is_admin claim — the handler must fall through to
        // the DB lookup. This is the pre-v1.2.0 migration path.
        AuthorizationHandlerContext context = BuildContext(
            userId, claimValue: null, otherClaims: new Dictionary<string, string>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "with the claim absent the handler must consult the DB " +
            "and the user row says IsAdmin=true.");
    }

    [Fact]
    public async Task CacheDisabled_ClaimTrue_StillReadsDb()
    {
        // The strict posture: even when the cached claim says
        // 'true' the handler must re-read the database, because
        // admin revocation must take effect on the very next
        // request. The DB row says the user is NOT an admin,
        // so the handler must NOT succeed.
        var users = new InMemoryUserRepository();
        Guid userId = Guid.NewGuid();
        await users.AddAsync(BuildUser(userId, isAdmin: false), TestContext.Current.CancellationToken);
        var handler = BuildHandler(users, cacheEnabled: false);

        AuthorizationHandlerContext context = BuildContext(
            userId, claimValue: "true", otherClaims: new Dictionary<string, string>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "with CacheAdminClaim=false the handler must ignore the " +
            "claim and rely on the live users row, which says " +
            "IsAdmin=false. Admin revocation must be immediate.");
    }

    [Fact]
    public async Task CacheDisabled_ClaimFalse_StillReadsDb()
    {
        var users = new InMemoryUserRepository();
        Guid userId = Guid.NewGuid();
        await users.AddAsync(BuildUser(userId, isAdmin: true), TestContext.Current.CancellationToken);
        var handler = BuildHandler(users, cacheEnabled: false);

        AuthorizationHandlerContext context = BuildContext(
            userId, claimValue: "false", otherClaims: new Dictionary<string, string>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "with CacheAdminClaim=false the handler reads the DB; " +
            "the user row says IsAdmin=true so the handler must " +
            "succeed even though the (stale) claim says false.");
    }

    [Fact]
    public async Task CacheDisabled_ClaimMissing_ReadsDb()
    {
        var users = new InMemoryUserRepository();
        Guid userId = Guid.NewGuid();
        await users.AddAsync(BuildUser(userId, isAdmin: true), TestContext.Current.CancellationToken);
        var handler = BuildHandler(users, cacheEnabled: false);

        AuthorizationHandlerContext context = BuildContext(
            userId, claimValue: null, otherClaims: new Dictionary<string, string>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UnauthenticatedPrincipal_IsIgnored()
    {
        var users = new InMemoryUserRepository();
        var handler = BuildHandler(users, cacheEnabled: true);

        ClaimsPrincipal anonymous = new(new ClaimsIdentity()); // not authenticated
        AuthorizationHandlerContext context = new(
            new[] { new AdminOnlyRequirement() },
            anonymous,
            resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    // ── helpers ──────────────────────────────────────────────

    private static AdminOnlyAuthorizationHandler BuildHandler(
        InMemoryUserRepository users, bool cacheEnabled) =>
        new(
            users,
            Options.Create(new AdminAuthorizationOptions { CacheAdminClaim = cacheEnabled }),
            NullLogger<AdminOnlyAuthorizationHandler>.Instance);

    private static AuthorizationHandlerContext BuildContext(
        Guid userId, string? claimValue, Dictionary<string, string> otherClaims)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (claimValue is not null)
        {
            claims.Add(new Claim(IsAdminClaim, claimValue));
        }
        foreach (KeyValuePair<string, string> other in otherClaims)
        {
            claims.Add(new Claim(other.Key, other.Value));
        }

        ClaimsIdentity identity = new(claims, authenticationType: "TestScheme");
        ClaimsPrincipal principal = new(identity);
        return new AuthorizationHandlerContext(
            new[] { new AdminOnlyRequirement() },
            principal,
            resource: null);
    }

    private static User BuildUser(Guid id, bool isAdmin)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        User user = User.Register(
            new UserId(id),
            Cardscape.Domain.Members.EmailAddress.Create($"{id:N}@cardscape.local").Value,
            Cardscape.Domain.Members.DisplayName.Create("Test User").Value,
            PasswordHash.FromHashed("v1.AAAA.BBBB").Value,
            now).Value;
        if (isAdmin)
        {
            user.SetAdmin(true, now);
        }
        return user;
    }
}
