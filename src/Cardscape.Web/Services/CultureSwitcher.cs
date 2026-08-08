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
/// <para>
/// BETA-8-UI-#3 + BETA-8-UI-#9 — see test-results/r8/r8-report.md.
/// The previous incarnation was a non-generic wrapper registered
/// only as <see cref="IStringLocalizer"/>. Components in this
/// app inject <see cref="IStringLocalizer{T}"/> (the generic
/// flavour, with <c>SharedResource</c> as the resource marker),
/// so the wrapper was never resolved: they got the raw
/// <c>StringLocalizer&lt;SharedResource&gt;</c> from the DI
/// container, which only knows about the embedded English
/// resx. Changing the picker updated the dictionary but every
/// @L["…"] expression still resolved to the English key. The
/// fix is to expose the wrapper under the generic interface too
/// (this class implements both) and re-register the DI mappings.
/// </para>
/// </summary>
public sealed class HttpBackedStringLocalizer<TResource> : IStringLocalizer<TResource>, IStringLocalizer
{
    private readonly StringLocalizer<TResource> _fallback;
    private readonly CultureSwitcher _switcher;

    public HttpBackedStringLocalizer(StringLocalizer<TResource> fallback, CultureSwitcher switcher)
    {
        // BETA-9-UI-#1 — see test-results/r9/r9-report.md.
        // The fallback is the framework's StringLocalizer<TResource>
        // (the standard resource manager-backed localizer that reads
        // the embedded SharedResource.resx). The previous
        // IStringLocalizer<TResource> parameter created a circular
        // DI dependency: every IStringLocalizer<SharedResource> was
        // mapped to this wrapper, so resolving the constructor
        // parameter would re-resolve the same wrapper indefinitely.
        // DI throws at start-up and the Blazor app shows the
        // unhandled-error overlay on every page. We depend on the
        // concrete type instead; Program.cs registers it under the
        // concrete name so the wrapper can take it as a dependency
        // without looping.
        _fallback = fallback;
        _switcher = switcher;
    }

    // The two interfaces share `this[string]` / `this[string, params object[]]`
    // / `GetAllStrings(bool)`. Implementing the public surface against the
    // generic interface and the non-generic one explicitly keeps the
    // compiler happy without forcing a runtime cast.
    public LocalizedString this[string name] => Lookup(name, arguments: null);

    public LocalizedString this[string name, params object[] arguments] => Lookup(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        EnumerateAll(includeParentCultures);

    private IEnumerable<LocalizedString> EnumerateAll(bool includeParentCultures)
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

    private LocalizedString Lookup(string name, object[]? arguments)
    {
        IReadOnlyDictionary<string, string> dict = _switcher.GetCurrentTranslations();
        if (dict.TryGetValue(name, out string? value))
        {
            return new LocalizedString(name, value, resourceNotFound: false);
        }

        // R10-UI-#2 — see test-results/r10/r10-report.md.
        // The resource manager's `this[string, params object[]]` overload
        // does `string.Format(value, arguments)` internally. When the
        // dictionary misses AND the caller did not pass args (e.g.
        // `L["HomeGreeting"]`), passing an empty `object[]` makes the
        // fallback throw `Format_IndexOutOfRange` on any value that has
        // a `{0}` placeholder — which is most of the greetings and
        // "Welcome back, {0}" messages. The dictionary hit path
        // returns the raw value (caller formats), so the fallback must
        // also return raw when no args were supplied, otherwise the two
        // paths disagree.
        return arguments is null
            ? _fallback[name]
            : _fallback[name, arguments];
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
/// See the comment on <see cref="HttpBackedStringLocalizer{TResource}"/>
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

    public CultureSwitcher(
        IHttpClientFactory httpClientFactory,
        IJSRuntime js,
        ILogger<CultureSwitcher> logger)
    {
        // The default HttpClient in Blazor WASM has no base address,
        // so a relative URL like `Resources/SharedResource.en.resx`
        // throws `net_http_client_invalid_requesturi` and the page
        // surfaces the unhandled-error UI for every navigation.
        // Inject the named `Cardscape.Resources` client registered
        // in Program.cs: its `BaseAddress` is set to the document
        // base, which makes the relative URL resolve correctly and
        // avoids the spurious error.
        _http = httpClientFactory.CreateClient("Cardscape.Resources");
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
        // BETA-8-UI-#3 + BETA-8-UI-#9 — see test-results/r8/r8-report.md.
        // Translations are now served by GET /api/internal/translate/{culture}
        // on the API. The previous path (a static /Resources/SharedResource.{c}.resx
        // file shipped as a Blazor static web asset) was never reachable
        // because the .resx lived under the Web project's Resources/ tree,
        // not wwwroot/, so the static-web-assets manifest never included it
        // and the Blazor client always 404'd the fetch. The new endpoint
        // reads the embedded SharedResource from the API assembly and
        // returns the parsed dictionary as JSON.
        if (string.Equals(culture, DefaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            // No HTTP fetch for English; the HttpBackedStringLocalizer
            // falls back to the embedded StringLocalizer<SharedResource>
            // which reads the assembly resource.
            return EmptyTranslations;
        }

        string url = $"api/internal/translate/{culture}";
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        using HttpResponseMessage response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        TranslationResponse? payload = await JsonSerializer.DeserializeAsync<TranslationResponse>(
            stream,
            TranslationJsonOptions);
        if (payload?.Translations is null)
        {
            return EmptyTranslations;
        }
        return new Dictionary<string, string>(payload.Translations, StringComparer.Ordinal);
    }

    private sealed record TranslationResponse(string Culture, IReadOnlyDictionary<string, string> Translations);

    // CA1869 — cache and reuse the options instance; deserialising
    // once per culture change is fine but we still don't want a
    // fresh JsonSerializerOptions each time.
    private static readonly JsonSerializerOptions TranslationJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, string> EmptyTranslations = new Dictionary<string, string>(StringComparer.Ordinal);
}
