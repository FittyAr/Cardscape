using Cardscape.Api.BackgroundJobs;
using Cardscape.Api.Endpoints.Activities;
using Cardscape.Api.Endpoints.Admin;
using Cardscape.Api.Endpoints.Ai;
using Cardscape.Api.Endpoints.Auth;
using Cardscape.Api.Endpoints.Automation;
using Cardscape.Api.Endpoints.BackgroundJobs;
using Cardscape.Api.Endpoints.Boards;
using Cardscape.Api.Endpoints.Cards;
using Cardscape.Api.Endpoints.Checklists;
using Cardscape.Api.Endpoints.Comments;
using Cardscape.Api.Endpoints.CustomFields;
using Cardscape.Api.Endpoints.Dashboards;
using Cardscape.Api.Endpoints.Dev;
using Cardscape.Api.Endpoints.Extensions;
using Cardscape.Api.Endpoints.Import;
using Cardscape.Api.Endpoints.Integrations;
using Cardscape.Api.Endpoints.Internal;
using Cardscape.Api.Endpoints.Labels;
using Cardscape.Api.Endpoints.Lists;
using Cardscape.Api.Endpoints.Notifications;
using Cardscape.Api.Endpoints.OAuth;
using Cardscape.Api.Endpoints.Recurrence;
using Cardscape.Api.Endpoints.Saml;
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
using Cardscape.Application.Realtime;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Infrastructure.Logging;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── JSON options ───────────────────────────────────────────
// Accept enum values as their string name in BOTH directions
// (request body deserialisation + response body serialisation).
// Without this, every endpoint that takes an `enum` body
// parameter (BoardVisibility, WorkspaceRole, etc.) returns a
// 500 from the model binder the moment a human types
// `"Private"` instead of `0` — see BUG #9 in
// test-results/BETA-TEST-REPORT.md. The Blazor WASM client
// always sends ints, so this only opens the door for external
// consumers (MCP, scripts, swagger "Try it out").
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(
            System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: true));
});

// ── Logging ────────────────────────────────────────────────
// Serilog is wired before every other service so config
// providers, EF Core, the hosted background dispatcher, and
// the rest of the request pipeline all see the structured
// logger. The browser side POSTs to /api/internal/client-log
// (mapped below) and the file / OTel / (future) DB sinks
// receive those events through the standard pipeline.
builder.UseCardscapeSerilog(ServiceType.Api);

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

builder.Services.AddCardscapeApplication(typeof(Program).Assembly);
builder.Services.AddCardscapeInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);

// ── Real-time (SignalR + MCP) ──────────────────────────────────
// Subscribed clients join board:{boardId} on demand. The
// DomainEventBroadcaster (static Wolverine handlers in
// Cardscape.Api.Realtime) bridges domain events from the
// Wolverine bus to the IBoardNotifier, which fans out to every
// SignalR connection in the matching group and, in parallel,
// pings the MCP process so AI clients that have subscribed to
// the matching board://{id} resource receive a
// notifications/resources/updated push. The MCP is reached
// through a typed HttpClient; the URL and shared secret are
// configured the same way as the reverse path
// (Cardscape:Mcp:BaseUrl + Internal:Secret).
builder.Services.AddSignalR();
builder.Services.AddSingleton<IBoardNotifier, CompositeBoardNotifier>();
builder.Services.AddSingleton<IMcpResourceNotifier, HttpMcpResourceNotifier>();
builder.Services.AddSingleton<McpSubscriptionsClient>();
builder.Services.AddHttpClient("Cardscape.Mcp", client =>
{
    string? mcpBaseUrl = builder.Configuration["Cardscape:Mcp:BaseUrl"]
        ?? builder.Configuration["Mcp:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("CARDS_CAPE__MCP__BASEURL")
        ?? "http://localhost:5292/";
    client.BaseAddress = new Uri(mcpBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

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

// Security headers run before everything else so the
// headers land on every response — including 401s, 429s,
// and the GlobalExceptionMiddleware 500 problem-details
// body. The middleware no-ops on per-request overrides so
// an endpoint that needs a relaxed policy (none today, but
// the seam is there) can still set its own value.
app.UseMiddleware<SecurityHeadersMiddleware>();

// ── Localization middleware ────────────────────────────────
// Honours the Accept-Language header (or the ?culture= query
// string override) and sets the current request culture for
// any IStringLocalizer<SharedResource> resolved inside an API
// endpoint. Sits before UseRouting so the rest of the pipeline
// sees the resolved culture.
app.UseRequestLocalization();

app.UseCors();

// ── Blazor WebAssembly client hosting ────────────────────────────────
// The Cardscape.Web project is referenced above so the SDK runs the
// static-web-assets merge and copies the compiled WASM client (wwwroot,
// index.html, _framework/*) into the API's output. These middlewares
// serve the framework files and static content; the fallback below
// lets Blazor's client-side router handle non-API URLs.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapDevOnlyEndpoints();
}

// Apply pending EF Core migrations on startup.
//
// In Development, this is unconditional (matches the historical behaviour).
// Outside Development (Staging, Production, …) the operator can still opt in
// per-deploy by setting `Cardscape:Database:RunMigrationsOnStartup=true`
// (env var: `Cardscape__Database__RunMigrationsOnStartup=true`). The default
// for non-Development is `true` so that the documented self-hostable
// `docker compose up` workflow actually produces a working schema instead
// of a 500-loop from the BackgroundJobDispatcher. Operators that prefer to
// run migrations as a separate step (e.g. via `dotnet ef database update`
// in a CI job) can flip the flag to `false` to opt out.
bool runMigrations = app.Configuration.GetValue("Cardscape:Database:RunMigrationsOnStartup",
    app.Environment.IsDevelopment());
if (runMigrations)
{
    app.ApplyMigrations();
}

app.UseHttpsRedirection();
app.UseAuthentication();

// BETA-3-#5 — see test-results/BETA-TEST-REPORT.md. The
// Idempotency-Key middleware sits after UseAuthentication so
// the JWT principal is already attached to HttpContext.User
// (the middleware reads the user id from the NameIdentifier
// claim; ICurrentUser is not populated until the auth handler
// runs). The response capture wraps the authorization + the
// endpoint so a 401/403/422 still goes through the replay
// path. It only acts on state-changing methods with the
// header present; GET / HEAD / OPTIONS are no-ops.
app.UseMiddleware<Cardscape.Api.Middleware.IdempotencyMiddleware>();

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
app.MapGoogleCalendarOAuthEndpoints();
app.MapScimEndpoints();
app.MapScimAdminEndpoints();
app.MapSamlEndpoints();
app.MapOAuthAppEndpoints();
app.MapOAuthFlowEndpoints();
app.MapAiEndpoints();
app.MapMcpSubscriptionsAdminEndpoints();
app.MapUserDsrAdminEndpoints();

// Companion endpoint for Serilog.Sinks.BrowserHttp on the
// Blazor WASM client. Browser-side log events (e.g. uncaught
// exceptions, navigation failures) are POSTed here in CLEF
// JSON; the endpoint re-emits them through the standard
// pipeline so the file / OTel sinks see them.
app.MapClientLogEndpoint();

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

namespace Cardscape.Api
{
    // Required for WebApplicationFactory<Cardscape.Api.Program>
    // in the E2E test project (the implicit Program class
    // that the .NET minimal-API SDK generates is internal;
    // the test project needs the type to be reachable).
    public partial class Program;
}

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
