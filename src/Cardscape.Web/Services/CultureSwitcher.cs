using System.Globalization;
using System.Resources;
using System.Text.Json;
using Cardscape.Web.Logging;
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
/// <remarks>
/// BETA-9-UI-#1 — see test-results/r9/r9-report.md. The fallback is the
/// framework's concrete <see cref="StringLocalizer{TResourceSource}"/>
/// (the resource manager-backed localizer that reads the embedded
/// SharedResource.resx). Depending on <c>IStringLocalizer&lt;TResource&gt;</c>
/// instead created a circular DI dependency: every
/// <c>IStringLocalizer&lt;SharedResource&gt;</c> is mapped to this wrapper,
/// so resolving the parameter re-resolved the same wrapper and DI threw at
/// start-up. Program.cs registers the concrete type so the wrapper can
/// take it without looping.
/// </remarks>
public sealed class HttpBackedStringLocalizer<TResource>(StringLocalizer<TResource> fallback, CultureSwitcher switcher)
    : IStringLocalizer<TResource>, IStringLocalizer
{
    private readonly StringLocalizer<TResource> _fallback = fallback;
    private readonly CultureSwitcher _switcher = switcher;

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
            if (arguments is not null && arguments.Length > 0)
            {
                try
                {
                    string formatted = string.Format(CultureInfo.CurrentCulture, value, arguments);
                    return new LocalizedString(name, formatted, resourceNotFound: false);
                }
                catch (FormatException)
                {
                    return new LocalizedString(name, value, resourceNotFound: false);
                }
            }

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
        return FromResources(name, arguments);
    }

    // The embedded .resx lookup must use the culture the user picked, not
    // CultureInfo.CurrentUICulture: WebAssembly loads the satellite
    // assembly for the browser language, and the render thread keeps the
    // browser culture (setting CurrentUICulture inside an async call does
    // not flow back to it). With a Spanish browser and English picked,
    // the dictionary above is empty for "en" and the ambient lookup
    // returned the Spanish satellite strings ("Bienvenido a Cardscape").
    private LocalizedString FromResources(string name, object[]? arguments)
    {
        string? value = Resources.GetString(name, CultureInfo.GetCultureInfo(_switcher.CurrentCulture));
        if (value is null)
        {
            return arguments is null ? _fallback[name] : _fallback[name, arguments];
        }

        if (arguments is not null && arguments.Length > 0)
        {
            try
            {
                value = string.Format(CultureInfo.CurrentCulture, value, arguments);
            }
            catch (FormatException)
            {
                // Keep the unformatted text, as the dictionary path does.
            }
        }

        return new LocalizedString(name, value, resourceNotFound: false);
    }

    private static readonly ResourceManager Resources = new(typeof(TResource));
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
public sealed class CultureSwitcher(
    IHttpClientFactory httpClientFactory,
    InstanceSettingsState instance,
    IJSRuntime js,
    ILogger<CultureSwitcher> logger)
{
    private const string StorageKey = "Cardscape.Culture";
    private const string DefaultCulture = "en";

    // The default HttpClient in Blazor WASM has no base address, so a
    // relative URL like `Resources/SharedResource.en.resx` throws
    // `net_http_client_invalid_requesturi`. The named client registered in
    // Program.cs has its BaseAddress set to the document base.
    private readonly HttpClient _http = httpClientFactory.CreateClient("Cardscape.Resources");
    private readonly InstanceSettingsState _instance = instance;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _translationsByCulture = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public event Func<Task>? Changed;

    public string CurrentCulture { get; private set; } = DefaultCulture;

    public IReadOnlyCollection<string> AvailableCultures { get; } = ["en", "es"];

    public IReadOnlyDictionary<string, string> GetCurrentTranslations()
    {
        return _translationsByCulture.TryGetValue(CurrentCulture, out IReadOnlyDictionary<string, string>? dict)
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

        // No saved choice → the administrator's default language
        // (System settings → General); if the instance settings are
        // unreachable, follow the browser language when we ship it.
        string browserLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        string instanceLanguage = (await _instance.GetAsync()).DefaultLanguage;
        string saved = AvailableCultures.Contains(instanceLanguage, StringComparer.OrdinalIgnoreCase)
            ? instanceLanguage
            : AvailableCultures.Contains(browserLanguage, StringComparer.OrdinalIgnoreCase)
                ? browserLanguage
                : DefaultCulture;
        try
        {
            string? fromStorage = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
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
            logger.SavedCultureReadFailed(ex, DefaultCulture);
        }

        await SetCultureAsync(saved, persist: false);
    }

    public async Task SetCultureAsync(string culture, bool persist = true)
    {
        culture = (culture ?? DefaultCulture).ToLowerInvariant();
        if (!AvailableCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
        {
            logger.UnknownCultureDefaulted(culture, DefaultCulture);
            culture = DefaultCulture;
        }

        if (!_translationsByCulture.ContainsKey(culture))
        {
            try
            {
                IReadOnlyDictionary<string, string> translations = await LoadTranslationsAsync(culture);
                _translationsByCulture[culture] = translations;
                logger.TranslationsLoaded(translations.Count, culture);
            }
            catch (Exception ex)
            {
                logger.TranslationsLoadFailed(ex, culture);
                _translationsByCulture[culture] = EmptyTranslations;
            }
        }

        CurrentCulture = culture;

        // Keep .NET's culture in step with the picker: the resx
        // fallback in HttpBackedStringLocalizer resolves through
        // CurrentUICulture, and dates / numbers format through
        // CurrentCulture.
        CultureInfo info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.DefaultThreadCurrentCulture = info;
        CultureInfo.DefaultThreadCurrentUICulture = info;
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;

        if (persist)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.setItem", StorageKey, culture);
            }
            catch (Exception ex)
            {
                logger.CulturePersistenceFailed(ex);
            }
        }

        await NotifyChangedAsync();
    }

    private Task NotifyChangedAsync() => Changed.InvokeSequentiallyAsync();

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
        using HttpResponseMessage response = await _http.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(CancellationToken.None);
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
