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

    public static DomainError InvalidPassword(string reason) =>
        DomainError.Validation("members.user.invalid_password", reason);
}
