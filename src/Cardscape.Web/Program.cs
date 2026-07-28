using System.Net.Http.Headers;
using Cardscape.Web;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Configuration (reads from wwwroot/appsettings.json) ─────────────
string apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is required (set in wwwroot/appsettings.json).");

// ── Auth + state providers ───────────────────────────────────────────
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AuthService>();

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

// ── Real-time (SignalR client) ──────────────────────────────
builder.Services.AddScoped<BoardHubClient>();

// Radzen component services.
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
