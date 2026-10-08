using Cardscape.Domain.Members;
using Cardscape.Domain.Members.Errors;
using Cardscape.Domain.Members.Events;

namespace Cardscape.UnitTests.Domain.Aggregates;

public sealed class UserEmailVerificationTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    private static User NewUser() => User.Register(
        UserId.New(),
        EmailAddress.Create("ada@example.test").Value,
        DisplayName.Create("Ada").Value,
        PasswordHash.FromHashed("hash").Value,
        At).Value;

    [Fact]
    public void Register_StartsUnverified()
    {
        NewUser().IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void VerifyEmail_WithTheIssuedToken_VerifiesAndClearsIt()
    {
        User user = NewUser();
        user.IssueEmailVerification("hash-1", At, Lifetime).IsSuccess.Should().BeTrue();

        user.VerifyEmail("hash-1", At.AddHours(1)).IsSuccess.Should().BeTrue();

        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(At.AddHours(1));
        user.EmailVerificationTokenHash.Should().BeNull();
        user.DomainEvents.OfType<UserEmailVerified>().Should().ContainSingle();
    }

    [Fact]
    public void VerifyEmail_RejectsAWrongOrReplacedToken()
    {
        User user = NewUser();
        user.IssueEmailVerification("old", At, Lifetime);
        user.IssueEmailVerification("new", At, Lifetime);

        user.VerifyEmail("old", At).Error.Should().Be(UserErrors.EmailVerificationInvalid);
        user.IsEmailVerified.Should().BeFalse();
        user.VerifyEmail("new", At).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void VerifyEmail_RejectsAnExpiredToken()
    {
        User user = NewUser();
        user.IssueEmailVerification("hash", At, Lifetime);

        user.VerifyEmail("hash", At + Lifetime).Error.Should().Be(UserErrors.EmailVerificationExpired);
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void IssueEmailVerification_RefusesAnAlreadyVerifiedAddress()
    {
        User user = NewUser();
        user.MarkEmailVerified(At);

        user.IssueEmailVerification("hash", At, Lifetime).Error.Should().Be(UserErrors.EmailAlreadyVerified);
    }

    [Fact]
    public void MarkEmailVerified_IsIdempotent_AndKeepsTheFirstTimestamp()
    {
        User user = NewUser();
        user.MarkEmailVerified(At);
        user.MarkEmailVerified(At.AddDays(1));

        user.EmailVerifiedAt.Should().Be(At);
        user.DomainEvents.OfType<UserEmailVerified>().Should().ContainSingle();
    }
}
