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
using Cardscape.Api.Endpoints.Extensions;
using Cardscape.Api.Endpoints.Internal;
using Cardscape.Api.Endpoints.Labels;
using Cardscape.Api.Endpoints.Lists;
using Cardscape.Api.Endpoints.Notifications;
using Cardscape.Api.Endpoints.Recurrence;
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
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidation();

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

app.UseCors();

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

// Real-time board hub. Sits at /hubs/board with the same JWT
// bearer authentication as the REST API; clients bring the
// access token in the query string (the SignalR client
// appends it automatically).
app.MapHub<BoardHub>("/hubs/board");

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
