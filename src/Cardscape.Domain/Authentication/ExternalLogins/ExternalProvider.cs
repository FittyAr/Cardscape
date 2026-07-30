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
    Apple = 3,
    Saml = 4
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
        ExternalProvider.Saml => "saml",
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
            case "saml": provider = ExternalProvider.Saml; return true;
            default: provider = default; return false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when this provider is fully wired
    /// in the current build. Apple requires
    /// <c>Authentication:Apple:TeamId</c>, <c>ClientId</c>,
    /// <c>KeyId</c> and <c>PrivateKeyPem</c> in configuration;
    /// when those are missing the OIDC handler is not
    /// registered and <see cref="IsImplemented"/> reports
    /// <c>false</c> so the UI hides the "Sign in with Apple"
    /// button. Google and Microsoft are complete.
    /// </summary>
    public static bool IsImplemented(this ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => true,
        ExternalProvider.Microsoft => true,
        ExternalProvider.Apple => true,
        _ => false
    };
}
