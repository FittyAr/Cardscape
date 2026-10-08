using Cardscape.Domain.Common;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Authentication;

/// <summary>The password rules for passwords people choose themselves.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static Result Check(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return Result.Failure(InvalidPassword($"Password must be at least {MinimumLength} characters long."));
        }

        return CommonPasswords.Set.Contains(password)
            ? Result.Failure(InvalidPassword("Password is on the breached-passwords list; pick a different one."))
            : Result.Success();
    }
}
