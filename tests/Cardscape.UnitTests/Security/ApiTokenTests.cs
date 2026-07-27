using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Security;

namespace Cardscape.UnitTests.Security;

public class ApiTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Stores_Hash_And_Prefix_And_Grants_Default_Expiry_Null()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("laptop").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read", "write" }).Value;

        var result = ApiToken.Create(userId, name, "deadbeef", "abcd1234", scopes, expiresAt: null, at: Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.HashedSecret.Should().Be("deadbeef");
        result.Value.SecretPrefix.Should().Be("abcd1234");
        result.Value.ExpiresAt.Should().BeNull();
        result.Value.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public void IsActive_Returns_False_When_Revoked()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("x").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read" }).Value;

        var token = ApiToken.Create(userId, name, "h", "p", scopes, null, Now).Value;
        token.Revoke(Guid.NewGuid(), "rotated", Now.AddMinutes(5));

        token.IsActive(Now.AddMinutes(6)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_Returns_False_When_Expired()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("x").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read" }).Value;

        var token = ApiToken.Create(userId, name, "h", "p", scopes, expiresAt: Now.AddMinutes(10), at: Now).Value;

        token.IsActive(Now.AddMinutes(9)).Should().BeTrue();
        token.IsActive(Now.AddMinutes(11)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_Records_Timestamp_And_Reason()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("x").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read" }).Value;

        var token = ApiToken.Create(userId, name, "h", "p", scopes, null, Now).Value;
        token.Revoke(Guid.NewGuid(), "left the company", Now.AddHours(1));

        token.RevokedAt.Should().Be(Now.AddHours(1));
        token.RevokedReason.Should().Be("left the company");
    }

    [Fact]
    public void Revoke_Already_Revoked_Returns_AlreadyRevoked()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("x").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read" }).Value;

        var token = ApiToken.Create(userId, name, "h", "p", scopes, null, Now).Value;
        token.Revoke(Guid.NewGuid(), null, Now).IsSuccess.Should().BeTrue();
        var second = token.Revoke(Guid.NewGuid(), null, Now.AddMinutes(1));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("security.api_token.already_revoked");
    }

    [Fact]
    public void Create_With_Empty_Hash_Fails()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("x").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read" }).Value;

        var result = ApiToken.Create(userId, name, "", "p", scopes, null, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Expiry_In_Past_Fails()
    {
        var userId = UserId.New();
        var name = ApiTokenName.Create("x").Value;
        var scopes = ApiTokenScopes.Create(new[] { "read" }).Value;

        var result = ApiToken.Create(userId, name, "h", "p", scopes, Now.AddSeconds(-1), Now);

        result.IsFailure.Should().BeTrue();
    }
}
