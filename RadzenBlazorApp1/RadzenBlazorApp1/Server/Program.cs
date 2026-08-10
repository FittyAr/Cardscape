using Radzen;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using Microsoft.AspNetCore.Components.Authorization;

using RadzenBlazorApp1.Server;    
using RadzenBlazorApp1.Server.Components;
using RadzenBlazorApp1.Server.Data;
using RadzenBlazorApp1.Server.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
      .AddInteractiveServerComponents().AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024)
      .AddInteractiveWebAssemblyComponents();

builder.Services.AddControllers();
builder.Services.AddRadzenComponents();
builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "RadzenBlazorApp1Theme";
    options.Duration = TimeSpan.FromDays(365);
});
builder.Services.AddHttpClient();


builder.Services.AddScoped<RadzenBlazorApp1.Server.CrmDBService>();
builder.Services.AddDbContext<CrmDBContext>(options =>
{
  options.UseSqlite(builder.Configuration.GetConnectionString("CrmDBConnection"));
});
builder.Services.AddControllers().AddOData(opt =>
{
    var oDataBuilderSample = new ODataConventionModelBuilder();
    oDataBuilderSample.EntitySet<RadzenBlazorApp1.Server.Models.CrmDB.Contact>("Contacts");
    oDataBuilderSample.EntitySet<RadzenBlazorApp1.Server.Models.CrmDB.CrmTask>("Tasks");
    oDataBuilderSample.EntitySet<RadzenBlazorApp1.Server.Models.CrmDB.Opportunity>("Opportunities");
    oDataBuilderSample.EntitySet<RadzenBlazorApp1.Server.Models.CrmDB.OpportunityStatus>("OpportunityStatuses");
    oDataBuilderSample.EntitySet<RadzenBlazorApp1.Server.Models.CrmDB.CrmTaskStatus>("TaskStatuses");
    oDataBuilderSample.EntitySet<RadzenBlazorApp1.Server.Models.CrmDB.TaskType>("TaskTypes");
    opt.AddRouteComponents("odata/CrmDB", oDataBuilderSample.GetEdmModel()).Count().Filter().OrderBy().Expand().Select().SetMaxTop(null).TimeZone = TimeZoneInfo.Utc;
});
builder.Services.AddScoped<RadzenBlazorApp1.Client.CrmDBService>();
builder.Services.AddHttpClient("RadzenBlazorApp1.Server").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false }).AddHeaderPropagation(o => o.Headers.Add("Cookie"));
builder.Services.AddHeaderPropagation(o => o.Headers.Add("Cookie"));
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddScoped<RadzenBlazorApp1.Client.SecurityService>();
builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
{
  options.UseSqlite(builder.Configuration.GetConnectionString("CrmDBConnection"));
});
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<ApplicationIdentityDbContext>().AddDefaultTokenProviders();
builder.Services.AddControllers().AddOData(o =>
{
    var oDataBuilder = new ODataConventionModelBuilder();
    oDataBuilder.EntitySet<ApplicationUser>("ApplicationUsers");
    var usersType = oDataBuilder.StructuralTypes.First(x => x.ClrType == typeof(ApplicationUser));
    usersType.AddProperty(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Password)));
    usersType.AddProperty(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.ConfirmPassword)));
    oDataBuilder.EntitySet<ApplicationRole>("ApplicationRoles");
    o.AddRouteComponents("odata/Identity", oDataBuilder.GetEdmModel()).Count().Filter().OrderBy().Expand().Select().SetMaxTop(null).TimeZone = TimeZoneInfo.Utc;
});
builder.Services.AddScoped<AuthenticationStateProvider, RadzenBlazorApp1.Client.ApplicationAuthenticationStateProvider>();

var app = builder.Build();


var forwardingOptions = new ForwardedHeadersOptions()
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardingOptions.KnownIPNetworks.Clear();
forwardingOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardingOptions);
    

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.MapControllers();
app.UseHeaderPropagation();
app.MapStaticAssets();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(RadzenBlazorApp1.Client._Imports).Assembly);
await app.SeedDataAsync();
app.Run();
