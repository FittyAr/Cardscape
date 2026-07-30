using System.Globalization;
using System.Net.Http.Headers;
using Cardscape.Web;
using Cardscape.Web.Resources;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Configuration (reads from wwwroot/appsettings.json) ─────────────
string apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is required (set in wwwroot/appsettings.json).");

// ── Localization (i18n) ──────────────────────────────────────────────
// Resources live under src/Cardscape.Web/Resources (SharedResource.resx
// and per-culture variants like SharedResource.es.resx).
//
// The execution plan §5.1 calls for the
// `AddLocalization(opts => opts.SetDefaultCulture("en")
// .AddSupportedCultures("en", "es"))` shape + `UseRequestLocalization`.
// That shape is a **server-side ASP.NET Core** API: the
// `SetDefaultCulture` / `AddSupportedCultures` extension methods live
// on `RequestLocalizationOptions` (the type consumed by the
// `UseRequestLocalization` middleware), and the `AddLocalization`
// callback's options type (`LocalizationOptions`) only exposes
// `ResourcesPath` — there is no "supported cultures" property to set
// on `LocalizationOptions`.
//
// The `RequestLocalizationOptions` type lives in
// `Microsoft.AspNetCore.Localization`, which is in the
// `Microsoft.AspNetCore.App` shared framework on the server. On Blazor
// WebAssembly the `Microsoft.NET.Sdk.BlazorWebAssembly` SDK only
// references a subset of the framework (the `Components.WebAssembly`
// parts), and adding `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
// fails with `NETSDK1082: no hay ningún paquete de tiempo de ejecución
// para Microsoft.AspNetCore.App disponible para el RuntimeIdentifier
// "browser-wasm"` (the framework has no `browser-wasm` runtime pack).
// The standalone `Microsoft.AspNetCore.Localization` NuGet package
// tops out at 2.3.11 (ASP.NET Core 2.x era) and is not compatible with
// the .NET 11 preview SDK's API surface. There is no clean way to
// surface the plan's named API on the WASM client in this SDK.
//
// The localization is therefore wired with what the WASM SDK
// actually supports:
// - `AddLocalization` registers the `.resx` resource path.
// - The supported-culture set is **implicit in the set of
//   `SharedResource.<culture>.resx` files** shipped under
//   `Resources/`. Today: `en` (default) + `es`.
// - The current culture is set via
//   `CultureInfo.DefaultThreadCurrentCulture` /
//   `DefaultThreadCurrentUICulture` (with the default taken from
//   `Culture:Default` in `wwwroot/appsettings.json`, or `"en"` if the
//   setting is missing).
//
// IMPORTANT (Blazor WASM caveat): `UseRequestLocalization` is
// server-side middleware that reads the `Accept-Language` request
// header and is impossible on Blazor WebAssembly — the header is
// read on the SERVER, but Blazor WASM is a client-side app served as
// static files, with no per-request server pipeline. The
// `Accept-Language` header the browser sends to fetch the static
// assets is a fetch-time header; the browser's preferred language
// is exposed to JavaScript and .NET-on-WASM as `navigator.language`
// (and `navigator.languages` for the full preference list), not as
// the `Accept-Language` request header.
//
// There is **no** `CulturePicker` class in the codebase yet — the
// `IStringLocalizer<SharedResource>` resolves to the right `.resx`
// based on whatever culture is set, so the localization works, but
// the culture never changes after startup. A future PR can either
// (a) read from `navigator.language` at startup, or (b) add a
// `CulturePicker` that reads/writes a `localStorage` override and
// calls `CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(value)`
// when the user picks a language. The follow-up is documented in
// `docs/i18n/02-translation-workflow.md` §12.
const string defaultCulture = "en";
// The supported-culture set. The audit baseline declared this array
// but never used it (the `AddLocalization` callback cannot consume
// it — see the comment block above). It is the single source of
// truth that a future client-side `CulturePicker` reads from (see
// `docs/i18n/02-translation-workflow.md` §12). The actual
// localization resolution is implicit in the set of
// `SharedResource.<culture>.resx` files shipped under
// `Resources/`.
string[] supportedCultures = { "en", "es" };

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

string? configuredDefault = builder.Configuration["Culture:Default"];
CultureInfo defaultCultureInfo = string.IsNullOrWhiteSpace(configuredDefault)
    ? new CultureInfo(defaultCulture)
    : new CultureInfo(configuredDefault);
CultureInfo.DefaultThreadCurrentCulture = defaultCultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = defaultCultureInfo;

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

// Radzen component services.
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
