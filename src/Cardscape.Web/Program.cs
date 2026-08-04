using System.Globalization;
using System.Net.Http.Headers;
using Cardscape.Web;
using Cardscape.Web.Logging;
using Cardscape.Web.Resources;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Logging ────────────────────────────────────────────────
// Serilog routes every ILogger<T> in the client through
// BrowserHttp (POSTs CLEF events to /api/internal/client-log
// on the API), the browser console, and the dev tools
// "Debug" sink. The API re-emits the received events so the
// file / OTel / (future) DB sinks all see browser-side logs.
builder.UseCardscapeBrowserSerilog();

// ── Configuration (reads from wwwroot/appsettings.json) ─────────────
string apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is required (set in wwwroot/appsettings.json).");

// ── Radzen component services + theme service ───────────────────────
// The cookie theme service persists the user's theme choice (default
// / dark / humanistic / material) in a cookie so it survives reloads
// and is available before the first render. It also feeds the
// `<RadzenTheme>` tag in index.html via `ThemeService.SetTheme` from
// the cookie if one is present.
builder.Services.AddRadzenComponents();
builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "CardscapeTheme";
    options.Duration = TimeSpan.FromDays(365);
});

// ── Localization (i18n) ──────────────────────────────────────────────
// Resources live under src/Cardscape.Web/Resources (SharedResource.resx
// and per-culture variants like SharedResource.es.resx).
//
// IMPORTANT: `ResourcesPath` is intentionally left as the empty string.
// The .NET 10 `ResourceManagerStringLocalizerFactory` computes the
// resource base name as
//   `{RootNamespace}.{ResourcesPath}.{TypeFullName − RootNamespace}`.
// With our setup (RootNamespace=`Cardscape.Web`, type in
// `Cardscape.Web.Resources`, default `ResourcesPath="Resources"`) the
// factory looks for `Cardscape.Web.Resources.Resources.SharedResource`
// — a manifest that does not exist, because the .NET SDK embeds the
// compiled .resx as `Cardscape.Web.Resources.SharedResource.resources`
// (i.e. the type's full name with no `ResourcesPath` prefix). The
// localizer swallows the `MissingManifestResourceException` and
// returns the resource key as the value, which is why the UI showed
// `AuthSignIn`, `AppName`, `HomePointBoardsTitle`, … instead of the
// actual translations. Setting `ResourcesPath=""` short-circuits the
// prefix and the localizer uses the type's full name as-is, which
// matches the SDK's manifest name. See
// `docs/i18n/02-translation-workflow.md` for the planned move to
// JSON-based localization (which uses `ResourcesPath` differently
// and will need its own configuration).
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = string.Empty;
});

// ── Culture switcher (D7 — v1.2.0 plan, G12 follow-up) ──────────────
// The picker fetches SharedResource.{culture}.resx static web assets
// and populates an in-memory dictionary; the HttpBackedStringLocalizer
// reads from that dictionary and falls back to the embedded English
// strings for the first render. This sidesteps the Blazor WASM
// culture-change-detection overlay (see
// src/Cardscape.Web/Services/CultureSwitcher.cs for the full
// rationale). The picker is a singleton; the localizer wraps the
// standard StringLocalizer<SharedResource> for the fallback path.
builder.Services.AddSingleton<Cardscape.Web.Services.CultureSwitcher>();
builder.Services.AddHttpClient("Cardscape.Resources", client =>
{
    // Same-origin; the base address is the page origin. We
    // explicitly set BaseAddress to a relative URL on the
    // server so the dev server's HTTPS proxy and the
    // production static-asset path both resolve.
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});
builder.Services.AddSingleton<Cardscape.Web.Services.HttpBackedStringLocalizer>();
builder.Services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizer>(sp =>
    sp.GetRequiredService<Cardscape.Web.Services.HttpBackedStringLocalizer>());

// ── Auth + state providers ───────────────────────────────────────────
builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IStringLocalizer<SharedResource>, StringLocalizer<SharedResource>>();

// ── HTTP client to the API ───────────────────────────────────────────
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddHttpClient("Cardscape.Api", (sp, client) =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddHttpMessageHandler<AuthTokenHandler>();

// ── Typed API clients (one per resource) ────────────────────────────
builder.Services.AddScoped<IWorkspacesApiClient, WorkspacesApiClient>();
builder.Services.AddScoped<IBoardsApiClient, BoardsApiClient>();
builder.Services.AddScoped<IListsApiClient, ListsApiClient>();
builder.Services.AddScoped<ICardsApiClient, CardsApiClient>();
builder.Services.AddScoped<ILabelsApiClient, LabelsApiClient>();
builder.Services.AddScoped<ICommentsApiClient, CommentsApiClient>();
builder.Services.AddScoped<ISecurityApiClient, SecurityApiClient>();
builder.Services.AddScoped<IInvitationsApiClient, InvitationsApiClient>();
builder.Services.AddScoped<INotificationsApiClient, NotificationsApiClient>();
builder.Services.AddScoped<IAutomationApiClient, AutomationApiClient>();
builder.Services.AddScoped<IGoogleCalendarApiClient, GoogleCalendarApiClient>();
builder.Services.AddScoped<IScimApiClient, ScimApiClient>();
builder.Services.AddScoped<IDashboardsApiClient, DashboardsApiClient>();
builder.Services.AddScoped<ISamlApiClient, SamlApiClient>();
builder.Services.AddScoped<IBoardExtensionsApiClient, BoardExtensionsApiClient>();
builder.Services.AddScoped<ICustomFieldsApiClient, CustomFieldsApiClient>();
builder.Services.AddScoped<IActivitiesApiClient, ActivitiesApiClient>();
builder.Services.AddScoped<IVotingApiClient, VotingApiClient>();
builder.Services.AddScoped<IChecklistsApiClient, ChecklistsApiClient>();
builder.Services.AddScoped<IRecurrenceApiClient, RecurrenceApiClient>();
builder.Services.AddScoped<IOAuthAppsApiClient, OAuthAppsApiClient>();
builder.Services.AddScoped<IAiApiClient, AiApiClient>();
builder.Services.AddScoped<ISlackApiClient, SlackApiClient>();
builder.Services.AddScoped<IGoogleDriveApiClient, GoogleDriveApiClient>();
builder.Services.AddScoped<IGitHubApiClient, GitHubApiClient>();
builder.Services.AddScoped<IEmailIntegrationApiClient, EmailIntegrationApiClient>();

// ── Real-time (SignalR client) ──────────────────────────────
builder.Services.AddScoped<BoardHubClient>();

await builder.Build().RunAsync();
