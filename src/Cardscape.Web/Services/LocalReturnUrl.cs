using System.Text.RegularExpressions;

namespace Cardscape.Web.Services;

/// <summary>
/// Sanitises a post-login return URL so a crafted
/// <c>?returnUrl=</c> cannot send the user to another origin
/// (open redirect). Only same-origin paths survive: either
/// root-relative (<c>/boards/1</c>) or base-relative
/// (<c>boards/1</c>, what <c>RedirectToLogin</c> produces).
/// Anything else falls back to <c>/</c>.
/// </summary>
public static partial class LocalReturnUrl
{
    public const string Fallback = "/";

    public static string Normalize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.Contains('\\')
            || returnUrl.Any(char.IsControl)
            || UriScheme().IsMatch(returnUrl))
        {
            return Fallback;
        }

        return returnUrl;
    }

    // "javascript:", "https:", "data:" … — a scheme makes the URL absolute.
    [GeneratedRegex(@"^\s*[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex UriScheme();
}
