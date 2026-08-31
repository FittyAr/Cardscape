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
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.DependencyInjection;
using Cardscape.Application.Realtime;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Infrastructure.Logging;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Seeder.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace Cardscape.Api.Hosting;

internal static class ApiHostServiceCollectionExtensions
{
    public static WebApplicationBuilder AddCardscapeApiHost(this WebApplicationBuilder builder)
    {
        // ── JSON options ───────────────────────────────────────────
        // Enums have one wire representation: camel-case names. Numeric
        // enum values are an implementation detail and are rejected so
        // reordering a CLR enum cannot silently change the API contract.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter(
                    System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: false));
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
        builder.Services.AddSingleton<HttpMcpResourceNotifier>();
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
        return builder;
    }
}

