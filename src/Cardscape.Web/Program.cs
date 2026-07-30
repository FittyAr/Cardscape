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
// IMPORTANT (Blazor WASM + .NET 11 preview SDK caveat): the SDK
// preview refuses to switch the culture at runtime. The original
// plan called for
//   AddLocalization(opts => opts.SetDefaultCulture("en")
//     .AddSupportedCultures("en", "es"))
// but that shape is a SERVER-side API (it lives on
// `RequestLocalizationOptions`, consumed by `UseRequestLocalization`,
// which doesn't run on Blazor WASM). Attempting to ship a
// `SetDefaultCulture` call (or a `BlazorWebAssemblyLoadAllGlobalizationData`
// workaround) on the WASM client in this SDK causes a startup
// culture-mismatch error every time the page is refreshed.
//
// Localization is therefore wired to the minimum that works on
// Blazor WASM today:
// - `AddLocalization` registers the `.resx` resource path.
// - The default culture is "en" (invariant); the localizer falls
//   back to `SharedResource.resx` for any UI culture.
// - The Spanish .resx is shipped as a static asset under
//   `wwwroot/Resources/` (NOT as an embedded resource) so a
//   future CulturePicker can load it client-side from
//   `navigator.language` / `localStorage` without the SDK bug
//   surfacing again. The .es file is currently dormant; the
//   localizer still uses English until a follow-up wires the
//   client-side picker. See `docs/i18n/02-translation-workflow.md`
//   for the follow-up plan.
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

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
