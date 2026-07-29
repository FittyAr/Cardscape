using Cardscape.Domain.Common;

namespace Cardscape.Domain.Authentication.ExternalLogins;

/// <summary>
/// The OAuth 2.0 / OIDC providers that the REST API
/// supports for external login. The wire name (returned by
/// <see cref="ExternalProviderExtensions.WireName(ExternalProvider)"/>)
/// matches the ASP.NET Core authentication scheme key the
/// application registers the handler under (and the segment
/// the client sends in the URL
/// <c>/api/auth/external/{provider}/start</c>).
/// </summary>
public enum ExternalProvider
{
    Google = 1,
    Microsoft = 2,
    Apple = 3
}

/// <summary>
/// String conversions for <see cref="ExternalProvider"/>. The
/// wire form is the lowercase scheme name (<c>google</c>,
/// <c>microsoft</c>, <c>apple</c>); the wire form is what
/// the REST endpoint accepts in the URL and what the
/// configuration keys are based on.
/// </summary>
public static class ExternalProviderExtensions
{
    public static string WireName(this ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => "google",
        ExternalProvider.Microsoft => "microsoft",
        ExternalProvider.Apple => "apple",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown external provider.")
    };

    public static bool TryParse(string? raw, out ExternalProvider provider)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            provider = default;
            return false;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "google": provider = ExternalProvider.Google; return true;
            case "microsoft": provider = ExternalProvider.Microsoft; return true;
            case "apple": provider = ExternalProvider.Apple; return true;
            default: provider = default; return false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when this provider is fully wired
    /// in the current build. Apple is a stub today (it
    /// requires generating a JWT <c>client_secret</c> per
    /// Apple's spec); Google and Microsoft are complete.
    /// </summary>
    public static bool IsImplemented(this ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => true,
        ExternalProvider.Microsoft => true,
        ExternalProvider.Apple => false, // TODO: wire Apple — needs JWT client_secret generation
        _ => false
    };
}
