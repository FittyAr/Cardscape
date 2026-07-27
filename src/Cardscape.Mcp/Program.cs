using Cardscape.Mcp.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ── Composition root ──────────────────────────────────────
//
// The MCP server composes the same Application + Infrastructure
// DI registrations as the REST API. The two services are
// independent deployables that share the application layer.
//
// For now (Phase 0 / Phase 1 setup), the application layer is
// not yet wired: there are no entity types, no DbContext, no
// concrete repositories. Phase 2 adds
// `AddCardscapeApplication` + `AddCardscapeInfrastructure` here
// alongside the first batch of MCP tools (workspaces_list,
// boards_list, boards_get, cards_list, cards_get, cards_create,
// cards_update, cards_move, cards_archive, comments_add,
// members_assign, search, labels_add, checklist_add).

builder.Services.AddCardscapeMcp(builder.Configuration);

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────
app.UseCardscapeMcp();
app.MapCardscapeHealthChecks();

app.Run();
