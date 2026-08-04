namespace Cardscape.Application.Authentication;

/// <summary>
/// Small, deliberately-curated list of the
/// most-leaked passwords. The list is used by
/// <see cref="Validations.RegisterUserCommandValidator"/>
/// to reject the obvious "password" / "12345678" /
/// "qwertyuiop" attempts that pass the
/// minimum-length check. The list is intentionally
/// short (the top 100) so the validator stays
/// fast; the canonical, larger list is the
/// <c>Have I Been Pwned</c> Pwned Passwords
/// top-100k set, but checking against a network
/// service in the registration path is out of
/// scope (and would itself be a privacy concern —
/// a registration request would leak the candidate
/// password to a third party). The local list
/// covers the long-tail of "human-default"
/// passwords that drive most credential-stuffing
/// success.
/// </summary>
public static class CommonPasswords
{
    public static readonly IReadOnlySet<string> Set = new HashSet<string>(StringComparer.Ordinal)
    {
        "12345678", "123456789", "1234567890", "1234567", "123456",
        "11111111", "00000000", "qwerty123", "qwertyuiop", "qwerty12",
        "password", "password1", "password12", "password123", "password1234",
        "password!", "Password", "Password1", "Password1!", "Password123",
        "Password1234", "Password1234!",
        "iloveyou1", "iloveyou12", "iloveyou123",
        "admin123", "admin1234", "administrator",
        "welcome1", "welcome12", "welcome123",
        "letmein1", "letmein12", "letmein123",
        "monkey123", "monkey1234", "dragon12", "dragon123",
        "baseball1", "baseball12", "football1", "football12",
        "shadow12", "shadow123", "master12", "master123",
        "michael1", "michael12", "jordan12", "jordan123",
        "jennifer", "jennifer1", "jennifer12",
        "hunter12", "hunter123", "hunter1234",
        "charlie1", "charlie12", "charlie123",
        "andrew12", "andrew123", "joshua12", "joshua123",
        "trustno1", "trustno12", "trustno123",
        "abc12345", "abc123456", "abcdef12", "abcdef123",
        "starwars", "starwars1", "starwars12",
        "computer", "computer1", "computer12",
        "whatever", "whatever1", "whatever12",
        "1q2w3e4r", "1q2w3e4r5t", "q1w2e3r4",
        "zaq12wsx", "zaq12wsxcde3",
        "asdf1234", "asdfghjkl", "asdfghjkl1",
        "zxcvbnm1", "zxcvbnm12", "zxcvbnm123",
        "qwer1234", "qwerasdf", "qwerasdfzxcv",
        "passw0rd", "passw0rd1", "passw0rd12",
        "P@ssw0rd", "P@ssw0rd1", "P@ssw0rd!",
        "princess1", "princess12", "princess123",
        "rockyou1", "rockyou12", "rockyou123",
        "ashley12", "ashley123", "ashley1234",
        "nicole12", "nicole123", "nicole1234",
        "daniel12", "daniel123", "daniel1234",
        "hello123", "hello1234", "freedom1", "freedom12",
        "summer12", "summer123", "summer1234",
        "love1234", "love12345",
        "test1234", "test12345", "test123456",
        "demo1234", "demo12345"
    };
}
