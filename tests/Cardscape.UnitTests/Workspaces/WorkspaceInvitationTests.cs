using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.UnitTests.Workspaces;

public class WorkspaceInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WorkspaceInvitation NewInvitation(
        TimeSpan? lifetime = null,
        string tokenHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        string tokenPrefix = "abc12345ab",
        string email = "alice@example.com")
    {
        var result = WorkspaceInvitation.Issue(
            workspaceId: WorkspaceId.New(),
            email: email,
            role: WorkspaceRole.Member,
            invitedBy: Guid.NewGuid(),
            tokenHash: tokenHash,
            tokenPrefix: tokenPrefix,
            at: Now,
            lifetime: lifetime);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Issue_Stores_Email_Lowercased_And_Trimmed()
    {
        var inv = NewInvitation(email: "  Alice@Example.COM  ");

        inv.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void Issue_Defaults_To_14_Day_Expiry()
    {
        var inv = NewInvitation();

        inv.ExpiresAt.Should().Be(Now.AddDays(WorkspaceInvitation.DefaultExpiryDays));
    }

    [Fact]
    public void Issue_Honours_Custom_Lifetime()
    {
        var inv = NewInvitation(lifetime: TimeSpan.FromDays(3));

        inv.ExpiresAt.Should().Be(Now.AddDays(3));
    }

    [Fact]
    public void Issue_Rejects_Empty_Email()
    {
        var result = WorkspaceInvitation.Issue(
            WorkspaceId.New(), email: "   ", WorkspaceRole.Member, Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "abc12345ab", Now, lifetime: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.invitation.email_required");
    }

    [Fact]
    public void Issue_Rejects_Bad_Hash_Length()
    {
        var result = WorkspaceInvitation.Issue(
            WorkspaceId.New(), "x@example.com", WorkspaceRole.Member, Guid.NewGuid(),
            tokenHash: "tooshort",
            tokenPrefix: "abc",
            at: Now, lifetime: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.invitation.token_hash_invalid");
    }

    [Fact]
    public void Issue_Rejects_Lifetime_Over_Max()
    {
        var result = WorkspaceInvitation.Issue(
            WorkspaceId.New(), "x@example.com", WorkspaceRole.Member, Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "abc12345ab", Now, lifetime: TimeSpan.FromDays(90));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.invitation.lifetime_invalid");
    }

    [Fact]
    public void IsActive_Returns_False_After_Accept()
    {
        var inv = NewInvitation();
        inv.Accept(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();

        inv.IsActive(Now.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_Returns_False_After_Revoke()
    {
        var inv = NewInvitation();
        inv.Revoke(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();

        inv.IsActive(Now.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_Returns_False_After_Expiry()
    {
        var inv = NewInvitation(lifetime: TimeSpan.FromDays(1));

        inv.IsActive(Now.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void Accept_After_Expiry_Returns_Expired()
    {
        var inv = NewInvitation(lifetime: TimeSpan.FromHours(1));

        var result = inv.Accept(Guid.NewGuid(), Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.invitation.expired");
    }

    [Fact]
    public void Accept_Twice_Returns_AlreadyAccepted()
    {
        var inv = NewInvitation();
        inv.Accept(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();

        var second = inv.Accept(Guid.NewGuid(), Now.AddMinutes(1));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("workspaces.invitation.already_accepted");
    }

    [Fact]
    public void Revoke_After_Accept_Returns_AlreadyAccepted()
    {
        var inv = NewInvitation();
        inv.Accept(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();

        var revoke = inv.Revoke(Guid.NewGuid(), Now.AddMinutes(1));

        revoke.IsFailure.Should().BeTrue();
        revoke.Error.Code.Should().Be("workspaces.invitation.already_accepted");
    }

    [Fact]
    public void Revoke_Twice_Returns_AlreadyRevoked()
    {
        var inv = NewInvitation();
        inv.Revoke(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();

        var second = inv.Revoke(Guid.NewGuid(), Now.AddMinutes(1));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("workspaces.invitation.already_revoked");
    }
}
