using Cardscape.Api.BackgroundJobs;
using Cardscape.Api.Endpoints.Activities;
using Cardscape.Api.Endpoints.Auth;
using Cardscape.Api.Endpoints.Automation;
using Cardscape.Api.Endpoints.BackgroundJobs;
using Cardscape.Api.Endpoints.Boards;
using Cardscape.Api.Endpoints.Cards;
using Cardscape.Api.Endpoints.Checklists;
using Cardscape.Api.Endpoints.Comments;
using Cardscape.Api.Endpoints.CustomFields;
using Cardscape.Api.Endpoints.Dashboards;
using Cardscape.Api.Endpoints.Extensions;
using Cardscape.Api.Endpoints.Import;
using Cardscape.Api.Endpoints.Integrations;
using Cardscape.Api.Endpoints.Internal;
using Cardscape.Api.Endpoints.Labels;
using Cardscape.Api.Endpoints.Lists;
using Cardscape.Api.Endpoints.Notifications;
using Cardscape.Api.Endpoints.Recurrence;
using Cardscape.Api.Endpoints.Scim;
using Cardscape.Api.Endpoints.Search;
using Cardscape.Api.Endpoints.Security;
using Cardscape.Api.Endpoints.Voting;
using Cardscape.Api.Endpoints.Workspaces;
using Cardscape.Api.Extensions;
using Cardscape.Api.Hubs;
using Cardscape.Api.Middleware;
using Cardscape.Api.Realtime;
using Cardscape.Application.DependencyInjection;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Infrastructure.Persistence;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Endpoint body types are nested as `private record class RenameBody`
    // inside each endpoint class. Multiple endpoint classes (Cards,
    // Lists, …) define records with the same short name, so the
    // default schemaId generator collides and the OpenAPI document
    // fails to build. Use the full type name (with `+` replaced by
    // `.`) as the schemaId so every body type is unique.
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});
builder.Services.AddValidation();

// ── Localization (i18n) ──────────────────────────────────────
// Resources live in src/Cardscape.Web/Resources; the API
// (which hosts the Blazor WASM client and the server-rendered
// fallbacks) also registers the localization services so an
// IStringLocalizer<SharedResource> resolved inside an endpoint
// returns the same culture-aware text the Web client picked.
// Supported cultures mirror the Web project; default is "en".
builder.Services.AddLocalization();
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    var supported = new[] { "en", "es" };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en");
    options.SupportedCultures = supported.Select(c => new System.Globalization.CultureInfo(c)).ToList();
    options.SupportedUICultures = supported.Select(c => new System.Globalization.CultureInfo(c)).ToList();
    options.RequestCultureProviders = new Microsoft.AspNetCore.Localization.IRequestCultureProvider[]
    {
        new Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider(),
        new Microsoft.AspNetCore.Localization.QueryStringRequestCultureProvider()
    };
});

builder.Services.AddCardscapeApplication();
builder.Services.AddCardscapeInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);

// ── Real-time (SignalR) ───────────────────────────────────────
// Subscribed clients join board:{boardId} on demand. The
// DomainEventBroadcaster (static Wolverine handlers in
// Cardscape.Api.Realtime) bridges domain events from the
// Wolverine bus to the IBoardNotifier, which fans out to every
// connection in the matching group.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IBoardNotifier, BoardNotifier>();

// ── Background job dispatcher (v0.7) ────────────────────────────
// Polls the background_jobs table for due work and dispatches it
// through Wolverine. See BackgroundJobDispatcherService for the
// concurrency story.
builder.Services.AddCardscapeBackgroundJobDispatcher(o =>
{
    o.PollInterval = TimeSpan.FromSeconds(2);
    o.BatchSize = 10;
});

var app = builder.Build();

// Resolve the registry and pull every IBackgroundJobHandler out
// of DI so the dispatcher can find them by type at runtime. This
// is a no-op until at least one handler is registered.
app.Services.UseCardscapeBackgroundJobHandlers();

// ── Middleware pipeline ─────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

// ── Localization middleware ────────────────────────────────
// Honours the Accept-Language header (or the ?culture= query
// string override) and sets the current request culture for
// any IStringLocalizer<SharedResource> resolved inside an API
// endpoint. Sits before UseRouting so the rest of the pipeline
// sees the resolved culture.
app.UseRequestLocalization();

app.UseCors();

// ── Blazor WebAssembly client hosting ────────────────────────────────
// The Cardscape.Web project is referenced above so its wwwroot (the
// compiled WASM client + index.html) is copied into the API's output
// by scripts/copy-blazor-client.ps1 (invoked by the API csproj's
// AfterTargets=Build target). These middlewares serve the framework
// files and static content; the fallback below lets Blazor's
// client-side router handle non-API URLs.
//
// The .NET 11 preview SDK only registers blazor.webassembly.js in the
// Web project's static web assets; the app's .wasm and the runtime
// .wasm/.js files end up in $(OutDir)/wwwroot/_framework but aren't
// auto-discovered. We add an extra UseStaticFiles with an explicit
// file provider pointing at the copy location so UseBlazorFrameworkFiles
// can resolve the rest of the framework files. This middleware must
// run before UseBlazorFrameworkFiles so the framework middleware only
// sees requests for files it doesn't already know about.
var clientWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(clientWwwroot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientWwwroot),
        ServeUnknownFileTypes = true,
        ContentTypeProvider = new FileExtensionContentTypeProvider()
    });
}
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.ApplyMigrations();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RateLimitMiddleware>();

// ── Endpoints ────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Cardscape.Api",
    timestamp = DateTime.UtcNow
})).WithName("HealthCheck").WithTags("Health").AllowAnonymous();

app.MapAuthEndpoints();
app.MapExternalLoginEndpoints();
app.MapWorkspaceEndpoints();
app.MapWorkspaceInvitationEndpoints();
app.MapBoardEndpoints();
app.MapListEndpoints();
app.MapCardEndpoints();
app.MapCommentEndpoints();
app.MapLabelEndpoints();
app.MapNotificationEndpoints();
app.MapActivityEndpoints();
app.MapSearchEndpoints();
app.MapSecurityEndpoints();
app.MapAutomationEndpoints();
app.MapCustomFieldEndpoints();
app.MapCustomFieldValueEndpoints();
app.MapVotingEndpoints();
app.MapChecklistEndpoints();
app.MapRecurrenceEndpoints();
app.MapBoardExtensionEndpoints();
app.MapBackgroundJobEndpoints();
app.MapBoardBroadcastEndpoints();
app.MapImportEndpoints();
app.MapTotpEndpoints();
app.MapDashboardsEndpoints();
app.MapGoogleDriveEndpoints();
app.MapGitHubEndpoints();
app.MapInboundEmailEndpoints();
app.MapSlackEndpoints();
app.MapGoogleCalendarEndpoints();
app.MapScimEndpoints();
app.MapScimAdminEndpoints();

// Real-time board hub. Sits at /hubs/board with the same JWT
// bearer authentication as the REST API; clients bring the
// access token in the query string (the SignalR client
// appends it automatically).
app.MapHub<BoardHub>("/hubs/board");

// Anything that didn't match an API endpoint or the static files above
// falls through to the Blazor client's index.html so its router can take
// over (e.g. /boards/123, /login, /workspaces).
app.MapFallbackToFile("index.html");

app.Run();

// Required for WebApplicationFactory in integration tests.
public partial class Program;

/// <summary>Local helper that applies pending migrations on startup in Development.</summary>
internal static class MigrationExtensions
{
    public static WebApplication ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        db.Database.Migrate();
        return app;
    }
}
