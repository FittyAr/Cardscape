using System.Globalization;
using System.Reflection;
using System.Resources;
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
    /// Reads the embedded <c>SharedResource.{culture}.resx</c>
    /// from the Cardscape.Web assembly and parses the
    /// <c>&lt;data name="…" /&gt;</c> elements into a
    /// dictionary. The .resx files are embedded by the SDK
    /// because the <c>.resx</c> siblings of
    /// <c>SharedResource.cs</c> are marked as
    /// <c>EmbeddedResource</c> in the Blazor project's
    /// default item group.
    /// </summary>
    private static Dictionary<string, string> LoadEmbeddedTranslations(string culture)
    {
        // BETA-8-UI-#3 + BETA-8-UI-#9 — see test-results/r8/r8-report.md.
        // The SDK only auto-embeds the *invariant* resx (the neutral
        // SharedResource.resources used by the IStringLocalizer fallback
        // path). Per-culture resx files (SharedResource.es.resx, …) are
        // NOT embedded in the assembly by default; the API csproj
        // copies them to the output directory as <Content> instead, and
        // the disk path below is the one that actually loads the
        // translations. We still try the embedded path first as a
        // fallback for the invariant culture, and as a safety net for
        // builds where someone did mark the culture-specific resx as
        // EmbeddedResource.
        string resourceName = $"Cardscape.Web.Resources.SharedResource.{culture}.resources";
        Assembly assembly = typeof(Cardscape.Web.Resources.SharedResource).Assembly;

        try
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using ResourceReader reader = new(stream);
                Dictionary<string, string> embedded = new(StringComparer.Ordinal);
                foreach (System.Collections.DictionaryEntry entry in reader)
                {
                    string? key = entry.Key as string;
                    string? value = entry.Value as string;
                    if (!string.IsNullOrEmpty(key) && value is not null)
                    {
                        embedded[key] = value;
                    }
                }
                if (embedded.Count > 0)
                {
                    return embedded;
                }
            }
        }
        catch
        {
            // Fall through to the disk path. The disk path is the
            // canonical source for non-invariant cultures today.
        }

        return LoadFromDisk(culture);
    }

    private static Dictionary<string, string> LoadFromDisk(string culture)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Resources", $"SharedResource.{culture}.resx");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

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
