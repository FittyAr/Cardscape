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
    extension(ExternalProvider provider)
    {
        public string WireName() => provider switch
        {
            ExternalProvider.Google => "google",
            ExternalProvider.Microsoft => "microsoft",
            ExternalProvider.Apple => "apple",
            ExternalProvider.Saml => "saml",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown external provider.")
        };

        /// <summary>Parses the wire name (case-insensitive, surrounding whitespace ignored).</summary>
        public static bool TryParse(string? raw, out ExternalProvider parsed)
        {
            ExternalProvider? match = raw?.Trim().ToLowerInvariant() switch
            {
                "google" => ExternalProvider.Google,
                "microsoft" => ExternalProvider.Microsoft,
                "apple" => ExternalProvider.Apple,
                "saml" => ExternalProvider.Saml,
                _ => null,
            };
            parsed = match.GetValueOrDefault();
            return match.HasValue;
        }
    }
}
