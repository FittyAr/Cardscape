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
    /// Returns <c>true</c> when this provider is known to the
    /// API surface (a valid value of the
    /// <see cref="ExternalProvider"/> enum that the
    /// external-login start endpoint accepts in the URL).
    ///
    /// <para>
    /// <b>NOTE:</b> this check used to be the only gate in
    /// the start endpoint, and it hard-coded <c>true</c> for
    /// Google / Microsoft / Apple. That meant an
    /// operator who had not supplied the
    /// <c>Authentication:Google:*</c> keys still got a
    /// 200-then-500 from <c>Results.Challenge</c> when the
    /// challenge couldn't find a registered "google"
    /// scheme — see BETA-2-#8 in
    /// test-results/BETA-TEST-REPORT.md.
    /// </para>
    ///
    /// <para>
    /// The check that actually matters for the
    /// challenge-to-scheme wiring now lives next to the
    /// endpoint (see <c>ExternalLoginEndpoints.cs</c>):
    /// the start endpoint reads
    /// <c>Microsoft.Extensions.Configuration.IConfiguration</c>
    /// directly and verifies the matching
    /// <c>Authentication:{Provider}:*</c> keys are
    /// populated before it calls <c>Results.Challenge</c>.
    /// </para>
    /// </summary>
    public static bool IsKnown(this ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => true,
        ExternalProvider.Microsoft => true,
        ExternalProvider.Apple => true,
        _ => false
    };
}
