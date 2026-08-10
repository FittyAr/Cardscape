using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Internal;

/// <summary>
/// Translation endpoint that pairs with
/// <c>Cardscape.Web.Services.CultureSwitcher</c> on the Blazor
/// client. The client used to fetch the raw
/// <c>SharedResource.{culture}.resx</c> as a static web asset,
/// but the static-web-assets manifest does not include files
/// that live outside the Blazor project's <c>wwwroot/</c>
/// tree, so the URL 404'd and the picker had nothing to load.
/// <para>
/// This endpoint reads the embedded <c>SharedResource</c>
/// resource for the requested culture from the API assembly
/// and returns the parsed key/value map as JSON. The client
/// receives the same shape it would have parsed from the
/// .resx XML, without depending on the static-file pipeline.
/// </para>
/// <para>
/// BETA-8-UI-#3 + BETA-8-UI-#9 — see test-results/r8/r8-report.md.
/// Anonymous on purpose: the same translations are public to
/// any signed-in user, and an unauthenticated client still
/// gets the English embedded copy (so the page renders
/// before sign-in). The risk of a translation leak is
/// negligible; the risk of a sign-in redirect storm is
/// significant.
/// </para>
/// </summary>
public static class TranslationEndpoint
{
    public static IEndpointRouteBuilder MapTranslationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/internal/translate/{culture}", (string culture) =>
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return Results.BadRequest(new { error = "Culture is required." });
            }

            culture = culture.ToLowerInvariant();
            if (!IsSupportedCulture(culture))
            {
                return Results.BadRequest(new { error = $"Culture '{culture}' is not supported." });
            }

            IReadOnlyDictionary<string, string> translations = LoadEmbeddedTranslations(culture);
            return Results.Ok(new
            {
                culture,
                translations
            });
        }).WithTags("Internal");

        return app;
    }

    private static bool IsSupportedCulture(string culture) =>
        culture is "en" or "es";

    /// <summary>
    /// Reads the per-culture <c>SharedResource.{culture}.resx</c>
    /// from the API's output directory and parses the
    /// <c>&lt;data name="…" /&gt;</c> elements into a
    /// dictionary. The .resx files are copied into the output
    /// directory by two <c>&lt;Content Include="..\Cardscape.Web\
    /// Resources\SharedResource.*.resx"&gt;</c> entries in the
    /// API csproj, so the loader does not need (and must not
    /// have) a project reference on the Web project — see the
    /// <c>Api_DependsOn_ApplicationInfrastructureDomain_Only</c>
    /// architecture test for the rule. The previous version
    /// tried the embedded-resource path first as a safety net
    /// for the invariant culture, but the embed was a side
    /// effect of the Blazor SDK's default item group and the
    /// disk path has been the canonical source since the
    /// <c>Content</c> entries landed (BETA-8-UI-#3 / #9 +
    /// BUG-A1-003).
    /// </summary>
    private static Dictionary<string, string> LoadEmbeddedTranslations(string culture)
    {
        // The name is historical — the method used to read the
        // embedded resource first; the disk path is now the
        // only path. Renaming the method would touch every
        // call site for no behavioural change, so the name
        // stays and the body is one line.
        _ = culture;
        return LoadFromDisk(culture);
    }

    private static Dictionary<string, string> LoadFromDisk(string culture)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Resources", $"SharedResource.{culture}.resx");
        if (File.Exists(path))
        {
            return ParseResx(path);
        }

        // BUG-A1-003 — see test-results/beta/round-2/reports/A1-auth.md.
        // The `en` culture is the invariant fallback in .NET
        // resource resolution, so the on-disk file is
        // `SharedResource.resx` (no culture suffix). Returning
        // an empty dictionary for `en` left the Blazor client
        // with a translation map that had no entries; every
        // label rendered as the raw resource key. The fix is
        // to fall back to the invariant file when the
        // culture-specific one is absent.
        if (culture == "en")
        {
            string invariantPath = Path.Combine(AppContext.BaseDirectory, "Resources", "SharedResource.resx");
            if (File.Exists(invariantPath))
            {
                return ParseResx(invariantPath);
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ParseResx(string path)
    {
        XDocument doc = XDocument.Load(path);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        Dictionary<string, string> dict = new(StringComparer.Ordinal);
        foreach (XElement data in doc.Descendants(ns + "data"))
        {
            string? name = data.Attribute("name")?.Value;
            string? value = data.Element(ns + "value")?.Value;
            if (!string.IsNullOrEmpty(name) && value is not null)
            {
                dict[name] = value;
            }
        }
        return dict;
    }
}
