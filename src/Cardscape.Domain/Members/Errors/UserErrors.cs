using Cardscape.Domain.Common;

namespace Cardscape.Domain.Members.Errors;

/// <summary>Common errors raised by the <c>Members</c> bounded context.</summary>
public static class UserErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("members.user.not_found", "User was not found.");

    public static readonly DomainError EmailAlreadyTaken =
        DomainError.Conflict("members.user.email_taken", "A user with this email already exists.");

    public static readonly DomainError InvalidCredentials =
        DomainError.Unauthenticated("members.user.invalid_credentials", "Invalid email or password.");

    public static readonly DomainError Inactive =
        DomainError.Forbidden("members.user.inactive", "This user account is deactivated.");

    public static readonly DomainError EmailAlreadyVerified =
        DomainError.Conflict("members.user.email_already_verified", "This email address is already verified.");

    public static readonly DomainError EmailVerificationInvalid =
        DomainError.Validation("members.user.email_verification_invalid", "The verification link is invalid.");

    public static readonly DomainError EmailVerificationExpired =
        DomainError.Validation("members.user.email_verification_expired", "The verification link has expired.");

    public static readonly DomainError EmailNotVerified =
        DomainError.Forbidden("members.user.email_not_verified", "Verify your email address first.");

    public static DomainError InvalidPassword(string reason) =>
        DomainError.Validation("members.user.invalid_password", reason);
}
