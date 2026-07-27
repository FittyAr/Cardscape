using Cardscape.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Services ─────────────────────────────────────────────
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Radzen component services.
builder.Services.AddRadzenComponents();

// TODO step 3 (out of scope for this commit):
// builder.Services.AddScoped<IBoardsApiClient, BoardsApiClient>();
// builder.Services.AddScoped<ICardsApiClient, CardsApiClient>();
// builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

await builder.Build().RunAsync();
