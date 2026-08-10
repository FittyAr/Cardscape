using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;

using RadzenBlazorApp1.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddRadzenComponents();

builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "RadzenBlazorApp1Theme";
    options.Duration = TimeSpan.FromDays(365);
});
builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<RadzenBlazorApp1.Client.CrmDBService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddHttpClient("RadzenBlazorApp1.Server", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("RadzenBlazorApp1.Server"));
builder.Services.AddScoped<RadzenBlazorApp1.Client.SecurityService>();
builder.Services.AddScoped<AuthenticationStateProvider, RadzenBlazorApp1.Client.ApplicationAuthenticationStateProvider>();

var host = builder.Build();
await host.RunAsync();