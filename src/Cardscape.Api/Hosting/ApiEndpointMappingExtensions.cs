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

internal static class ApiEndpointMappingExtensions
{
    public static WebApplication MapCardscapeEndpoints(this WebApplication app)
    {
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
        return app;
    }
}

