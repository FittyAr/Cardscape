using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Cardscape.Web.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Cardscape.Web.Services;

/// <summary>
/// D7 (v1.2.0 plan) — G12 follow-up. Client-side
/// <see cref="IStringLocalizer{T}"/> that reads translations
/// from a process-wide in-memory dictionary populated by the
/// <see cref="CultureSwitcher"/> at startup and on every
/// language change.
/// <para>
/// The dictionary is empty on first render. The
/// <see cref="CultureSwitcher"/> populates the dictionary on
/// every state change via <see cref="CultureSwitcher.SetCultureAsync"/>,
/// which fetches the matching <c>SharedResource.{culture}.resx</c>
/// static web asset via <see cref="HttpClient"/> and parses the
/// <c>&lt;data name="…" /&gt;</c> elements into the dictionary.
/// Until the first fetch resolves, the localizer falls back to
/// the embedded <c>StringLocalizer&lt;SharedResource&gt;</c>
/// (English) so the first render is never empty.
/// </para>
/// <para>
/// Why this dance: the v1.1.0 G12 push tried to wire
/// <c>SetDefaultCulture</c> / <c>AddSupportedCultures</c> +
/// <c>CultureInfo.DefaultThreadCurrentCulture</c>, but the
/// <c>Blazor detected a change in the application's culture
/// that is not supported with the current project configuration</c>
/// overlay fires on every F5 refresh because the .NET 10 SDK
/// does not support the WASM-side culture-change-detection
/// override. This service sidesteps the runtime invariant by
/// not touching <see cref="System.Threading.Thread.CurrentCulture"/>
/// at all — the translations live in a dictionary the
/// <see cref="IStringLocalizer"/> reads from, the runtime culture
/// stays at <see cref="CultureInfo.InvariantCulture"/>, and the
/// Blazor culture-change detection never fires.
/// </para>
/// </summary>
public sealed class HttpBackedStringLocalizer : IStringLocalizer
{
    private readonly IStringLocalizer _fallback;
    private readonly CultureSwitcher _switcher;

    public HttpBackedStringLocalizer(IStringLocalizer<SharedResource> fallback, CultureSwitcher switcher)
    {
        // The generic localizer is the standard
        // StringLocalizer<SharedResource> that reads the
        // embedded SharedResource.resx (English). It is the
        // fallback for the first render before the picker
        // has loaded the static .resx.
        _fallback = (IStringLocalizer)fallback;
        _switcher = switcher;
    }

    public LocalizedString this[string name] => Lookup(name, arguments: null);

    public LocalizedString this[string name, params object[] arguments] => Lookup(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        foreach (KeyValuePair<string, string> pair in _switcher.GetCurrentTranslations())
        {
            yield return new LocalizedString(pair.Key, pair.Value, resourceNotFound: false);
        }

        // Always include the fallback (English) strings so
        // the dictionary can be partially populated and
        // still show all the keys.
        foreach (LocalizedString s in _fallback.GetAllStrings(includeParentCultures))
        {
            if (_switcher.GetCurrentTranslations().ContainsKey(s.Name))
            {
                continue;
            }
            yield return s;
        }
    }

    public IStringLocalizer WithCulture(CultureInfo? culture) => this;

    private LocalizedString Lookup(string name, object[]? arguments)
    {
        IReadOnlyDictionary<string, string> dict = _switcher.GetCurrentTranslations();
        if (dict.TryGetValue(name, out string? value))
        {
            return new LocalizedString(name, value, resourceNotFound: false);
        }
        return _fallback[name, arguments ?? Array.Empty<object>()];
    }
}

/// <summary>
/// D7 (v1.2.0 plan) — G12 follow-up. Service that owns the
/// current UI culture for the Blazor WASM client.
/// <para>
/// The service is a singleton. On startup, the Blazor layout
/// calls <see cref="InitializeAsync"/> which reads the saved
/// culture from <c>localStorage</c> (if any) and loads the
/// matching <c>SharedResource.{culture}.resx</c> static web
/// asset into the in-memory dictionary.
/// </para>
/// <para>
/// The <see cref="SetCultureAsync"/> method is called by the
/// language switcher in <c>MainLayout.razor</c>. It persists
/// the choice to <c>localStorage</c>, loads the new
/// translations, and raises <see cref="Changed"/> so the
/// layout can re-render.
/// </para>
/// <para>
/// The service does not touch
/// <see cref="System.Threading.Thread.CurrentCulture"/>; the
/// runtime culture stays at <see cref="CultureInfo.InvariantCulture"/>
/// and the localizer reads translations from the dictionary.
/// See the comment on <see cref="HttpBackedStringLocalizer"/>
/// for the full rationale.
/// </para>
/// </summary>
public sealed class CultureSwitcher
{
    private const string StorageKey = "Cardscape.Culture";
    private const string DefaultCulture = "en";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly ILogger<CultureSwitcher> _logger;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _translationsByCulture = new(StringComparer.OrdinalIgnoreCase);
    private string _currentCulture = DefaultCulture;
    private bool _initialized;

    public CultureSwitcher(HttpClient http, IJSRuntime js, ILogger<CultureSwitcher> logger)
    {
        // Use a named client so the request is treated as a
        // static asset fetch (same-origin relative URL). The
        // default HttpClient is fine here; the named-client
        // "Cardscape.Api" carries the API base URL and bearer
        // token and would 401 on a relative /Resources/ path.
        _http = http;
        _js = js;
        _logger = logger;
    }

    public event Action? Changed;

    public string CurrentCulture => _currentCulture;

    public IReadOnlyCollection<string> AvailableCultures { get; } = new[] { "en", "es" };

    public IReadOnlyDictionary<string, string> GetCurrentTranslations()
    {
        return _translationsByCulture.TryGetValue(_currentCulture, out IReadOnlyDictionary<string, string>? dict)
            ? dict
            : EmptyTranslations;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        string saved = DefaultCulture;
        try
        {
            string? fromStorage = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(fromStorage) && AvailableCultures.Contains(fromStorage, StringComparer.OrdinalIgnoreCase))
            {
                saved = fromStorage;
            }
        }
        catch (Exception ex)
        {
            // Pre-render or JS not available yet. Fall back
            // to the default; the layout will call
            // SetCultureAsync on the first user interaction.
            _logger.LogWarning(ex, "Could not read saved culture from localStorage; defaulting to {DefaultCulture}.", DefaultCulture);
        }

        await SetCultureAsync(saved, persist: false);
    }

    public async Task SetCultureAsync(string culture, bool persist = true)
    {
        culture = (culture ?? DefaultCulture).ToLowerInvariant();
        if (!AvailableCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unknown culture {Culture}; defaulting to {DefaultCulture}.", culture, DefaultCulture);
            culture = DefaultCulture;
        }

        if (!_translationsByCulture.ContainsKey(culture))
        {
            try
            {
                IReadOnlyDictionary<string, string> translations = await LoadTranslationsAsync(culture);
                _translationsByCulture[culture] = translations;
                _logger.LogInformation("Loaded {Count} translations for culture {Culture}.", translations.Count, culture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load translations for culture {Culture}; falling back to embedded English.", culture);
                _translationsByCulture[culture] = EmptyTranslations;
            }
        }

        _currentCulture = culture;

        if (persist)
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, culture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist culture to localStorage.");
            }
        }

        Changed?.Invoke();
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTranslationsAsync(string culture)
    {
        // The static asset is at /Resources/SharedResource.{culture}.resx
        // (packaged from src/Cardscape.Web/Resources/SharedResource.es.resx
        // per the <None Pack="true"> directive in the .csproj).
        string url = $"Resources/SharedResource.{culture}.resx";
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        using HttpResponseMessage response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        XDocument doc = XDocument.Load(stream);
        Dictionary<string, string> dict = new(StringComparer.Ordinal);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
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

    private static readonly IReadOnlyDictionary<string, string> EmptyTranslations = new Dictionary<string, string>(StringComparer.Ordinal);
}
