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
// and per-culture variants like SharedResource.es.resx). Blazor
// WebAssembly does not run the server-side UseRequestLocalization
// middleware; the client picks the culture explicitly via the
// CulturePicker and stores the choice in localStorage. The default
// culture is "en"; the supported set is the union of every
// SharedResource.<culture>.resx file shipped.
const string defaultCulture = "en";
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
builder.Services.AddScoped<IBoardExtensionsApiClient, BoardExtensionsApiClient>();
builder.Services.AddScoped<ICustomFieldsApiClient, CustomFieldsApiClient>();
builder.Services.AddScoped<IActivitiesApiClient, ActivitiesApiClient>();
builder.Services.AddScoped<IVotingApiClient, VotingApiClient>();
builder.Services.AddScoped<IChecklistsApiClient, ChecklistsApiClient>();
builder.Services.AddScoped<IRecurrenceApiClient, RecurrenceApiClient>();

// ── Real-time (SignalR client) ──────────────────────────────
builder.Services.AddScoped<BoardHubClient>();

// Radzen component services.
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
