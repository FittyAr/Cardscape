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

internal static class ApiApplicationExtensions
{
    public static WebApplication ConfigureCardscapePipeline(this WebApplication app)
    {
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

        app.MapCardscapeEndpoints();

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
        return app;
    }

    private static WebApplication ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
        db.Database.Migrate();
        return app;
    }
}



