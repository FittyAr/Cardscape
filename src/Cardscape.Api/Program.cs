using Cardscape.Api.BackgroundJobs;
using Cardscape.Api.Endpoints.Activities;
using Cardscape.Api.Endpoints.Admin;
using Cardscape.Api.Endpoints.Ai;
using Cardscape.Api.Endpoints.Attachments;
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
using Cardscape.Api.Endpoints.Seeder;
using Cardscape.Api.Endpoints.UserPreferences;
using Cardscape.Api.Endpoints.Users;
using Cardscape.Api.Endpoints.Voting;
using Cardscape.Api.Endpoints.Webhooks;
using Cardscape.Api.Endpoints.Workspaces;
using Cardscape.Api.Extensions;
using Cardscape.Api.Hubs;
using Cardscape.Api.Middleware;
using Cardscape.Api.OpenApi;
using Cardscape.Api.Realtime;
using Cardscape.Application.DependencyInjection;
using Cardscape.Application.Realtime;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Infrastructure.Logging;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Seeder.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

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
// consumers (MCP, scripts, the Scalar "Try it out" panel).
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
// Native .NET 10+ OpenAPI document generation
// (Microsoft.AspNetCore.OpenApi). The generator emits the
// document at /openapi/v1.json when MapOpenApi() is called
// (Development only) and uses the type's full name as the
// schema id, so the nested `private record class RenameBody`
// types in endpoint classes no longer collide — the
// Swashbuckle-era CustomSchemaIds workaround is gone. The
// Bearer security scheme is contributed by
// BearerSecuritySchemeTransformer; Scalar renders the
// "Authorize" button and the padlock on every endpoint that
// goes through RequireAuthorization() without any further
// wiring.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer<CardBodySchemasTransformer>();
    options.AddDocumentTransformer<WebhookEventsSchemaTransformer>();
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

// ── Seeder (optional, feature-gated) ────────────────────────
// Registered unconditionally so the lifetime graph
// (singleton SeedRunner, singleton SeedReport, transient
// steps) is consistent across runs. The toggle
// (Cardscape:Seeder:Enabled) is read at request time so
// flipping it requires only a config reload / restart.
// The Seeder is a headless library; the /admin/seeder UI
// lives in Cardscape.Web (Blazor WASM) which polls the
// /api/admin/seeder/* JSON surface this project exposes.
builder.Services.AddCardscapeSeeder(builder.Configuration);

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
    // /openapi/v1.json — the OpenAPI document.
    // /scalar           — the Scalar API reference UI.
    // Both are wired on top of the same document the rest of the
    // pipeline (release job, third-party SDK generators) consumes.
    app.MapOpenApi();
    app.MapScalarApiReference();
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
app.MapWebhookEndpoints();
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
// BUG-A5-002 — see test-results/beta/reports/A5-card-extras.md.
// Wires the new /api/cards/{id}/attachments/* surface that the
// domain already had but the API never exposed. The
// multipart upload is bounded to 30 MB at the framework
// level (DisableRequestSizeLimit stays off; the per-card
// upload goes through ReadFormAsync).
app.MapAttachmentEndpoints();
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
app.MapUserPreferencesEndpoints();
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
app.MapUserSelfEndpoints();

// ── Seeder (JSON surface only) ──────────────────────────────
// The Seeder is a headless library; the JSON endpoints
// under /api/admin/seeder/* are what the Web's Blazor
// admin page polls. Self-gated on Cardscape:Seeder:Enabled:
// when the flag is off, every route returns 404 and the
// UI is invisible.
app.MapSeederEndpoints();

// Companion endpoint for Serilog.Sinks.BrowserHttp on the
// Blazor WASM client. Browser-side log events (e.g. uncaught
// exceptions, navigation failures) are POSTed here in CLEF
// JSON; the endpoint re-emits them through the standard
// pipeline so the file / OTel sinks see them.
//
// BETA-8-UI-#2 — see test-results/r8/r8-report.md.
// The endpoint was previously registered TWICE in this file.
// ASP.NET Core's router raises AmbiguousMatchException on
// every POST and returns 500, which the Blazor renderer
// surfaces as the persistent 'An unhandled error has
// occurred' overlay on every page. Keep the single
// registration.
app.MapClientLogEndpoint();

// Translation relay for the Blazor client's CultureSwitcher.
// The static-web-assets manifest does not include .resx files
// from the Web project's Resources/ tree, so the client cannot
// fetch them directly. This endpoint reads the embedded
// SharedResource for the requested culture and returns the
// parsed key/value map as JSON. See TranslationEndpoint.cs
// for the rationale and BETA-8-UI-#3 in
// test-results/r8/r8-report.md.
app.MapTranslationEndpoint();

// Real-time board hub. Sits at /hubs/board with the same JWT
// bearer authentication as the REST API; clients bring the
// access token in the query string (the SignalR client
// appends it automatically).
app.MapHub<BoardHub>("/hubs/board");

// Anything that didn't match an API endpoint or the static files above
// falls through to the Blazor client's index.html so its router can take
// over (e.g. /boards/123, /login, /workspaces).
//
// BETA-5-#11 — see test-results/BETA-TEST-REPORT.md.
// The original MapFallbackToFile matched every unmatched path, so a
// malformed API URL like /api/boards/not-a-guid (the :guid
// route constraint rejects the segment, so no API endpoint
// matches) was falling through to the Blazor SPA and returning
// 200 + index.html. The API was effectively hiding 4xx responses
// for malformed GUIDs and any other routing typos. The scoped
// fallback only handles non-/api paths, so missing API routes
// now bubble up to 404 as the client expects.
//
// /openapi/* and /scalar are also excluded: MapOpenApi()/MapScalarApiReference()
// are endpoint registrations, not middleware, so the sub-pipeline
// below would otherwise intercept the request and return index.html
// (200 with the SPA shell) before the routing middleware ever sees
// the OpenAPI document or Scalar UI endpoints.
app.MapWhen(
    context => !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseStaticFiles().UseRouting().UseEndpoints(endpoints =>
    {
        endpoints.MapFallbackToFile("index.html");
    }));

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
