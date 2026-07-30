# Audit 04 — Integrations (Priority 3, §3.7–3.10)

**Date:** 2026-07-30
**Scope:** Slack, Google Drive, GitHub, Email-to-board integrations
**Plan reference:** `docs/roadmap/03-execution-plan-v1.1.0.md` §3.7–3.10
**Auditor:** general-purpose agent

---

## Summary table

| § | Item | Verdict | Why |
|---|------|---------|-----|
| 3.7 | Slack | **PARTIAL** (DRIFT) | All bounded context, abstractions, HTTP service, broadcaster, REST endpoints, MCP tools, migration tables are present. **DI registration of `ISlackNotificationService` and the two Slack repositories is MISSING** in `InfrastructureServiceCollectionExtensions.cs`, so any call that resolves them would crash at runtime. Dedicated `/workspaces/{id}/integrations/slack` page is MISSING — the hub page (`WorkspaceIntegrations.razor:20`) navigates to it but the page does not exist. |
| 3.8 | Google Drive | **PARTIAL** (DRIFT) | All bounded context, abstractions, HTTP picker service, REST endpoints, MCP tools, migration tables are present. **DI registration of `IGoogleDrivePickerService` and `IGoogleDriveConnectionRepository` is MISSING**. Dedicated Web UI page is MISSING; the hub page (`WorkspaceIntegrations.razor:27`) routes to `/settings/integrations/google-calendar` (which is the Google Calendar page, not Google Drive). The picker URL is built as an OAuth `accounts.google.com` URL (correctly per docs) but the "callback" endpoint from the plan (`/api/integrations/google/callback`) is not implemented — the picker returns the file id to the SPA, which then POSTs to `/api/integrations/google/attach` instead. |
| 3.9 | GitHub | **PARTIAL** (DRIFT) | All bounded context, abstractions, HTTP service, REST endpoints, MCP tools, migration tables are present. **DI registration of `IGitHubService`, `IGitHubRepoLinkRepository`, and `IGitHubPullRequestLinkRepository` is MISSING**. Dedicated Web UI page is MISSING; the hub page (`WorkspaceIntegrations.razor:34`) navigates to `/workspaces/{id}/integrations/github` which does not exist. The "GitHub section in the card menu" referenced in the plan is MISSING. The "OAuth flow under /api/integrations/github/connect" is implemented as a JSON-POST "link repo" endpoint (`IntegrationsEndpoints.cs:69`), not a real OAuth handshake — the plan wording is ambiguous so this may be acceptable. |
| 3.10 | Email-to-board | **PARTIAL** (DRIFT) | All bounded context, abstractions, HTTP service, REST endpoints, MCP tool, migration tables are present. **DI registration of `IInboundEmailService` and `IInboundEmailAddressRepository` is MISSING**. Dedicated Web UI page is MISSING; the hub page (`WorkspaceIntegrations.razor:41`) navigates to `/workspaces/{id}/integrations/email` which does not exist. The "webhook secret" mentioned in the plan is not surfaced in the API surface (the implementation accepts the body verbatim and trusts the address is registered, per `DefaultInboundEmailService.cs:69`). |

The four items have a **shared, CRITICAL gap**: none of the integration abstractions or repositories are registered with DI in `InfrastructureServiceCollectionExtensions.cs:51-285` or in any other composition root. The implementations exist and compile, but any runtime call that resolves them (`IMessageBus.InvokeAsync` from the API endpoints, or from the MCP tools) will throw `InvalidOperationException: No service for type '…' has been registered`. This is invisible to the build and to the existing unit tests because the integration paths are not exercised.

A second shared gap: the **migration is consolidated** as a single `V110IntegrationConsolidated` (`20260730000751_V110IntegrationConsolidated.cs`) that creates all six new tables (dashcards, github_pull_request_links, github_repo_links, google_drive_connections, inbound_email_addresses, slack_channels, slack_workspaces). The plan calls for separate `IssueSlackIntegrations` and `IssueInboundEmailAddresses` migrations; this is a DRIFT in migration naming, not a functional gap — the resulting schema is the same and `CardscapeDbContextModelSnapshot.cs:1052,1104,1224,1279,1535,1593` includes all six tables.

No integration tests exist for any of the four features (`tests/Cardscape.IntegrationTests/` is empty; the only functional test is `GoldenPathSmokeTests.cs` which does not exercise the integration paths).

---

## 3.7 — Slack integration — PARTIAL (DRIFT)

**Plan requirements vs evidence:**

- ✅ **Bounded context** `Cardscape.Domain.Integrations.Slack/`
  - `src/Cardscape.Domain/Integrations/Slack/SlackWorkspace.cs:16`
  - `src/Cardscape.Domain/Integrations/Slack/SlackChannel.cs:15`
  - `src/Cardscape.Domain/Integrations/Slack/SlackWorkspaceId.cs`
  - `src/Cardscape.Domain/Integrations/Slack/SlackChannelId.cs`
  - `src/Cardscape.Domain/Integrations/Slack/SlackEventFilter.cs:10` (the `SlackEventTypes` catalogue)

- ✅ **`ISlackNotificationService`** in Application with HTTP implementation
  - `src/Cardscape.Application/Abstractions/Integrations/ISlackNotificationService.cs:14`
  - `src/Cardscape.Infrastructure/Integrations/HttpSlackNotificationService.cs:24`
  - Posts to `https://slack.com/api/chat.postMessage` (`HttpSlackNotificationService.cs:26-27, 73-77`).
  - Bot token from `Integrations:Slack:BotToken` configuration (`HttpSlackNotificationService.cs:37`).

- ✅ **Webhook events**: `card.created`, `card.moved`, `card.completed`, `comment.added`
  - The `SlackEventTypes` catalogue defines all four (`SlackEventFilter.cs:12-15`).
  - `SlackEventBroadcaster` (`src/Cardscape.Application/Integrations/Slack/SlackEventBroadcaster.cs:24`) handles all four: `CardCreated` (line 26), `CardMoved` (line 56), `CardCompleted` (line 86), `CommentAdded` (line 116).
  - The broadcaster fans each event out to every active Slack channel mapping subscribed to that event type (`SlackEventBroadcaster.cs:146-191`).

- ⚠️ **Migration `IssueSlackIntegrations`** — DRIFT
  - The plan calls for a dedicated `IssueSlackIntegrations` migration. The actual migration is `V110IntegrationConsolidated` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260730000751_V110IntegrationConsolidated.cs:9`) which creates all six new tables including `slack_workspaces` (line 146) and `slack_channels` (line 122).
  - The schema is correct (both tables + their indexes are in `CardscapeDbContextModelSnapshot.cs:1535,1593`), so this is a naming DRIFT, not a functional gap.

- ❌ **Web UI: `/workspaces/{id}/integrations/slack`** — MISSING
  - The hub page links to it: `src/Cardscape.Web/Pages/WorkspaceIntegrations.razor:20` (`Nav.NavigateTo($"/workspaces/{WorkspaceId}/integrations/slack")`).
  - No page exists with that route. The `src/Cardscape.Web/Pages/` directory has no `WorkspaceIntegrationsSlack.razor` (verified by listing — only `WorkspaceIntegrations.razor` exists).

- ✅ **REST endpoints** (under `/api/workspaces/{id}/integrations/slack`)
  - `GET /` — `src/Cardscape.Api/Endpoints/Integrations/SlackEndpoints.cs:27` (get workspace connection)
  - `POST /connect` — `src/Cardscape.Api/Endpoints/Integrations/SlackEndpoints.cs:34`
  - `GET /channels` — `src/Cardscape.Api/Endpoints/Integrations/SlackEndpoints.cs:45`
  - `POST /channels` — `src/Cardscape.Api/Endpoints/Integrations/SlackEndpoints.cs:52`
  - `DELETE /channels/{channelId}` — `src/Cardscape.Api/Endpoints/Integrations/SlackEndpoints.cs:64`
  - Mounted from `src/Cardscape.Api/Program.cs:196` (`app.MapSlackEndpoints()`).

- ✅ **MCP tools** (all three present in `src/Cardscape.Mcp/Tools/IntegrationsTools.cs`):
  - `integrations_slack_connect` — `IntegrationsTools.cs:29`
  - `integrations_slack_list_channels` — `IntegrationsTools.cs:40`
  - `integrations_slack_unlink_channel` — `IntegrationsTools.cs:51`

- ❌ **CRITICAL — DI registration missing**
  - The Slack abstractions + repositories are NOT registered in `src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (verified by `Select-String` — no matches for `Slack` in that file).
  - `ISlackNotificationService` is not registered, so `HttpSlackNotificationService` cannot be resolved.
  - `ISlackChannelRepository` and `ISlackWorkspaceRepository` are not registered, so the repositories cannot be resolved.
  - At runtime, `ConnectSlackWorkspaceCommand` (which is invoked from both the REST endpoint and the MCP tool) will fail with `InvalidOperationException: No service for type 'ISlackWorkspaceRepository' has been registered.`

- **Notes**:
  - The OAuth flow is implemented as a JSON POST that the SPA sends with the bot token in the body (`SlackEndpoints.cs:34-43` and `ConnectSlackWorkspaceCommand.cs:17-21`). The plan's wording is consistent with this (`/workspaces/{id}/integrations/slack` page with "the OAuth connect button"), so this is acceptable.
  - The `SlackEventBroadcaster` is a static class with static Wolverine handlers — it does not need explicit DI registration; Wolverine discovers it via `opts.Discovery.IncludeAssembly(assembly)` in `ApplicationServiceCollectionExtensions.cs:27`. ✅
  - Bot token is hashed (SHA-256, `ConnectSlackWorkspaceCommand.cs:97-101`) before persistence; cleartext is sourced from configuration at call time. Good practice.
  - No integration tests cover the Slack broadcaster or endpoints. The four required webhooks (card.created, card.moved, card.completed, comment.added) have no test that asserts they fan out to Slack.

---

## 3.8 — Google Drive integration — PARTIAL (DRIFT)

**Plan requirements vs evidence:**

- ✅ **Bounded context** `Cardscape.Domain.Integrations.GoogleDrive/`
  - `src/Cardscape.Domain/Integrations/GoogleDrive/GoogleDriveConnection.cs:13` (per-user refresh token)
  - `src/Cardscape.Domain/Integrations/GoogleDrive/GoogleDriveConnectionId.cs`

- ✅ **`IGoogleDrivePickerService`** in Application with HTTP implementation
  - `src/Cardscape.Application/Abstractions/Integrations/IGoogleDrivePickerService.cs:14`
  - `src/Cardscape.Infrastructure/Integrations/HttpGoogleDrivePickerService.cs:33`
  - `BuildPickerUrlAsync` returns a `https://accounts.google.com/o/oauth2/v2/auth` URL (`HttpGoogleDrivePickerService.cs:88-95`) with `state` encoding the (workspace, user) tuple.
  - `AttachFileAsync` exchanges the refresh token for an access token (`HttpGoogleDrivePickerService.cs:130, 221-263`), downloads the file, persists it via `IStorageService` (`HttpGoogleDrivePickerService.cs:193`), and creates a new `Attachment` on the card (`HttpGoogleDrivePickerService.cs:196-208`).

- ⚠️ **OAuth flow `/api/integrations/google/connect`, `/api/integrations/google/callback`** — PARTIAL
  - `/api/integrations/google/connect` is mounted as both `GET` and `POST` in `IntegrationsEndpoints.cs:32-48` (the GET returns the picker URL, the POST records the connection). This is reasonable for an SPA-driven flow.
  - `/api/integrations/google/callback` is **NOT mounted**. The implementation assumes the SPA captures the picker response and POSTs to `/api/integrations/google/attach` instead (`IntegrationsEndpoints.cs:50-57`). This is functionally equivalent but the plan called for an explicit callback endpoint.

- ✅ **REST endpoints** (under `/api/integrations/google`)
  - `GET /connect` — `IntegrationsEndpoints.cs:32`
  - `POST /connect` — `IntegrationsEndpoints.cs:41`
  - `POST /attach` — `IntegrationsEndpoints.cs:50`
  - Mounted from `src/Cardscape.Api/Program.cs:193`.

- ✅ **MCP tools** (both present in `IntegrationsTools.cs`):
  - `integrations_google_drive_picker_url` — `IntegrationsTools.cs:63`
  - `integrations_google_drive_attach` — `IntegrationsTools.cs:73`

- ⚠️ **Migration** — DRIFT (consolidated into `V110IntegrationConsolidated`; see §3.7 for context). The `google_drive_connections` table is created in the same consolidated migration (`20260730000751_V110IntegrationConsolidated.cs:78-98`); the snapshot at `CardscapeDbContextModelSnapshot.cs:1224` reflects it.

- ❌ **CRITICAL — DI registration missing**
  - `IGoogleDrivePickerService` is not registered (no `HttpGoogleDrivePickerService` registration in `InfrastructureServiceCollectionExtensions.cs`).
  - `IGoogleDriveConnectionRepository` is not registered.
  - `GoogleDriveConnectionRepository` itself is implemented in `src/Cardscape.Infrastructure/Repositories/GoogleDriveConnectionRepository.cs:8` but is not bound to its interface anywhere.
  - At runtime, `GetGoogleDrivePickerUrlQuery`, `AttachGoogleDriveFileCommand`, and `ConnectGoogleDriveCommand` will all fail with DI-resolution errors.

- ❌ **Web UI** — MISSING
  - The hub page routes to the wrong page: `src/Cardscape.Web/Pages/WorkspaceIntegrations.razor:27` navigates to `/settings/integrations/google-calendar`, which is the Google **Calendar** page (`src/Cardscape.Web/Pages/GoogleCalendar.razor:1`), not Google **Drive**.
  - There is no `GoogleDriveApiClient` in `src/Cardscape.Web/Services/Api/` (only `GoogleCalendarApiClient.cs` exists, which uses paths `api/integrations/google-calendar/...`).
  - No dedicated `/workspaces/{id}/integrations/google-drive` (or similar) page exists.

- **Notes**:
  - Configuration keys are read at runtime: `Integrations:Google:ClientId`, `Integrations:Google:ClientSecret`, `Integrations:Google:RedirectUri` (`HttpGoogleDrivePickerService.cs:72, 224-225, 267-270`). The service degrades gracefully with a `DomainError.External` when configuration is missing.
  - Refresh token is stored encrypted via `ISecretProtector` (`HttpGoogleDrivePickerService.cs:121`) and decrypted on demand.
  - No integration tests cover the picker flow.

---

## 3.9 — GitHub integration — PARTIAL (DRIFT)

**Plan requirements vs evidence:**

- ✅ **Bounded context** `Cardscape.Domain.Integrations.GitHub/`
  - `src/Cardscape.Domain/Integrations/GitHub/GitHubRepoLink.cs:14` (board → repo, event filter)
  - `src/Cardscape.Domain/Integrations/GitHub/GitHubRepoLinkId.cs`
  - `src/Cardscape.Domain/Integrations/GitHub/GitHubEventTypes.cs:10` (catalogue)
  - `src/Cardscape.Domain/Integrations/GitHub/GitHubPullRequestLink.cs:136` (the per-card PR link)
  - `src/Cardscape.Domain/Integrations/GitHub/GitHubPullRequestLinkId.cs`

- ✅ **`IGitHubService`** in Application with HTTP implementation
  - `src/Cardscape.Application/Abstractions/Integrations/IGitHubService.cs:15`
  - `src/Cardscape.Infrastructure/Integrations/HttpGitHubService.cs:21`
  - Methods: `ListBranchesAsync`, `ListPullRequestsAsync`, `ListIssuesAsync`, `CreateIssueAsync` — all four exist (`HttpGitHubService.cs:44, 84, 132, 182`).
  - Uses `https://api.github.com` base URL with a personal-access token from `Integrations:GitHub:Token` (`HttpGitHubService.cs:23, 33-41`).

- ⚠️ **OAuth flow `/api/integrations/github/connect`** — PARTIAL
  - The plan's "OAuth flow" is implemented as a JSON POST that the SPA sends to `/api/integrations/github/connect` to register a (board, repoFullName, events) tuple (`IntegrationsEndpoints.cs:69-76`). The `IGitHubService` itself uses a server-side PAT, so this is a per-board "link" rather than a per-user OAuth handshake. The plan wording is ambiguous, so this is acceptable.
  - Full OAuth handshake (with state, code, callback) is **not** implemented.

- ✅ **REST endpoints** (under `/api/integrations/github`)
  - `POST /connect` — `IntegrationsEndpoints.cs:69` (link a repo to a board)
  - `GET /pulls` — `IntegrationsEndpoints.cs:78`
  - `POST /pulls/link` — `IntegrationsEndpoints.cs:86`
  - `POST /issues` — `IntegrationsEndpoints.cs:96`
  - Mounted from `src/Cardscape.Api/Program.cs:194`.

- ✅ **MCP tools** (all four present in `IntegrationsTools.cs`):
  - `integrations_github_list_prs` — `IntegrationsTools.cs:86`
  - `integrations_github_list_issues` — `IntegrationsTools.cs:97`
  - `integrations_github_link_pr` — `IntegrationsTools.cs:108`
  - `integrations_github_create_issue` — `IntegrationsTools.cs:119`

- ⚠️ **Migration** — DRIFT (consolidated into `V110IntegrationConsolidated`; see §3.7). Both `github_repo_links` and `github_pull_request_links` are created in the same migration (`20260730000751_V110IntegrationConsolidated.cs:36-55, 57-76`); the snapshot includes them at `CardscapeDbContextModelSnapshot.cs:1052,1104`.

- ❌ **CRITICAL — DI registration missing**
  - `IGitHubService` is not registered (no `HttpGitHubService` registration anywhere).
  - `IGitHubRepoLinkRepository` and `IGitHubPullRequestLinkRepository` are not registered.
  - `GitHubRepoLinkRepository` is implemented at `src/Cardscape.Infrastructure/Repositories/GitHubRepoLinkRepository.cs:9` (along with `GitHubPullRequestLinkRepository` at line 47) but neither is bound to its interface.
  - At runtime, every GitHub REST endpoint and every MCP tool will fail with DI-resolution errors.

- ❌ **Web UI** — MISSING
  - The hub page routes to a non-existent page: `src/Cardscape.Web/Pages/WorkspaceIntegrations.razor:34` navigates to `/workspaces/{WorkspaceId}/integrations/github`, but no such page exists (verified by listing the `src/Cardscape.Web/Pages/` directory).
  - The "GitHub section in the card menu" referenced in the plan is NOT implemented in `src/Cardscape.Web/Pages/CardDetail.razor` (verified by grep — no `GitHub` references).
  - No `GitHubApiClient` in `src/Cardscape.Web/Services/Api/`.

- **Notes**:
  - PR links are stored per-card (`github_pull_request_links` table) and a `IGitHubPullRequestLinkRepository` is defined in `src/Cardscape.Application/Abstractions/Persistence/IGitHubRepoLinkRepository.cs:23`.
  - The implementation filters out GitHub pull requests from the issues list (`HttpGitHubService.cs:159`).
  - Configuration key: `Integrations:GitHub:Token` (PAT). The service degrades gracefully with a `DomainError.External` when the token is missing.
  - No integration tests cover the GitHub flow.

---

## 3.10 — Email-to-board — PARTIAL (DRIFT)

**Plan requirements vs evidence:**

- ✅ **Bounded context** `Cardscape.Domain.Integrations.InboundEmail/`
  - `src/Cardscape.Domain/Integrations/InboundEmail/InboundEmailAddress.cs:17` (workspace → email address, target list)
  - `src/Cardscape.Domain/Integrations/InboundEmail/InboundEmailAddressId.cs`
  - Note: there is also a duplicate-looking file at `src/Cardscape.Domain/Integrations/Email/InboundEmailAddress.cs` (and `Email/InboundEmailAddressId.cs`). The plan and the MCP tool import the `InboundEmail` namespace; the `Email` namespace is unused. This is a small DRIFT in folder naming — both files exist; the actual `InboundEmailAddress` aggregate is imported from `InboundEmail` in `IntegrationsTools.cs:5`, `InboundEmailCommands.cs:10`, `DefaultInboundEmailService.cs:8`. The duplicate is dead code.

- ✅ **`IInboundEmailService`** in Application with default implementation
  - `src/Cardscape.Application/Abstractions/Integrations/IInboundEmailService.cs:12`
  - `src/Cardscape.Infrastructure/Integrations/DefaultInboundEmailService.cs:36`
  - Parses SendGrid, Mailgun, and Postmark webhook formats (`DefaultInboundEmailService.cs:95-188`).
  - Resolves the destination address to a workspace + list (`DefaultInboundEmailService.cs:68-75`) and dispatches `CreateCardCommand` via the Wolverine bus (`DefaultInboundEmailService.cs:82-86`).

- ✅ **REST endpoint** `POST /api/integrations/email/inbound`
  - `src/Cardscape.Api/Endpoints/Integrations/IntegrationsEndpoints.cs:148` — accepts a raw body, normalises provider from query/header (`IntegrationsEndpoints.cs:159-161`), and dispatches `HandleInboundEmailCommand` (`IntegrationsEndpoints.cs:163-164`).
  - The endpoint is **public** (no `RequireAuthorization()`) per the same file (line 109-111 mounts the group without auth; only `/addresses` and friends are authed via the inner `authed` group, line 114-116).
  - The plan called for SendGrid / Mailgun / Postmark signature verification — the implementation does NOT verify request signatures; it accepts the body verbatim and trusts the address is registered. This is a **security gap** (any caller who knows a registered `cardscape+board-…@in.example.com` address can create cards).

- ✅ **MCP tool** `integrations_email_list_addresses`
  - `src/Cardscape.Mcp/Tools/IntegrationsTools.cs:132`
  - Backed by `ListInboundEmailAddressesQuery` (`src/Cardscape.Application/Integrations/InboundEmail/Queries/InboundEmailQueries.cs:11`).

- ✅ **Other REST endpoints** (config surface, authed)
  - `GET /addresses` — `IntegrationsEndpoints.cs:118`
  - `POST /addresses` — `IntegrationsEndpoints.cs:125`
  - `DELETE /addresses/{addressId}` — `IntegrationsEndpoints.cs:135`
  - Mounted from `src/Cardscape.Api/Program.cs:195`.

- ⚠️ **Migration `IssueInboundEmailAddresses`** — DRIFT (consolidated into `V110IntegrationConsolidated`). The `inbound_email_addresses` table is created at `20260730000751_V110IntegrationConsolidated.cs:100-120`; the snapshot includes it at `CardscapeDbContextModelSnapshot.cs:1279`.

- ❌ **CRITICAL — DI registration missing**
  - `IInboundEmailService` is not registered.
  - `IInboundEmailAddressRepository` is not registered.
  - `InboundEmailAddressRepository` is implemented at `src/Cardscape.Infrastructure/Repositories/InboundEmailAddressRepository.cs:8` but is not bound to its interface.
  - At runtime, every email endpoint and the MCP tool will fail with DI-resolution errors.

- ❌ **Web UI** — MISSING
  - The hub page routes to a non-existent page: `src/Cardscape.Web/Pages/WorkspaceIntegrations.razor:41` navigates to `/workspaces/{WorkspaceId}/integrations/email`, but no such page exists.
  - The "webhook secret" the plan mentioned is not surfaced in the REST API; the implementation has no concept of a per-address secret. The REST surface is register / list / unregister / inbound-POST, with no secret returned to the client.

- **Notes**:
  - Mailgun form-encoded payloads are parsed by hand (`DefaultInboundEmailService.cs:152-167`) — small surface, no `WebUtilities` import.
  - No integration tests cover the inbound email flow.

---

## Cross-cutting gaps

### 1. CRITICAL — DI registration missing for all 4 integrations

Verified by `Select-String` against `src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`: zero matches for `Slack`, `GitHub`, `GoogleDrive`, or `InboundEmail`. The Google Calendar service (`HttpGoogleCalendarSyncService`) is registered at `InfrastructureServiceCollectionExtensions.cs:262-264` (per line 262-264); none of the 4 plan items have an equivalent.

Missing registrations (would need to be added to the same file):

```
services.AddScoped<ISlackWorkspaceRepository, SlackWorkspaceRepository>();
services.AddScoped<ISlackChannelRepository, SlackChannelRepository>();
services.AddHttpClient<ISlackNotificationService, HttpSlackNotificationService>();

services.AddScoped<IGoogleDriveConnectionRepository, GoogleDriveConnectionRepository>();
services.AddHttpClient<IGoogleDrivePickerService, HttpGoogleDrivePickerService>();

services.AddScoped<IGitHubRepoLinkRepository, GitHubRepoLinkRepository>();
services.AddScoped<IGitHubPullRequestLinkRepository, GitHubRepoLinkRepository>(); // same file
services.AddHttpClient<IGitHubService, HttpGitHubService>();

services.AddScoped<IInboundEmailAddressRepository, InboundEmailAddressRepository>();
services.AddScoped<IInboundEmailService, DefaultInboundEmailService>();
```

`HttpGoogleDrivePickerService` depends on `ISecretProtector`, `IStorageService`, `ICardRepository`, `IUnitOfWork`, `IClock` — all already registered. The other three `Http*` services take `(HttpClient, IConfiguration)` only and don't need further deps.

Until this is fixed, the integrations would crash on first use. The build and the 427 unit tests pass because the integration paths are not exercised by any test.

### 2. DRIFT — migration naming

The plan calls for `IssueSlackIntegrations` (per §3.7) and `IssueInboundEmailAddresses` (per §3.10). The actual migration is a single `V110IntegrationConsolidated` (`20260730000751_V110IntegrationConsolidated.cs`) that creates all six new tables (dashcards, github_pull_request_links, github_repo_links, google_drive_connections, inbound_email_addresses, slack_channels, slack_workspaces). The resulting schema is correct; this is purely a naming DRIFT. The model snapshot (`CardscapeDbContextModelSnapshot.cs:1052, 1104, 1224, 1279, 1535, 1593`) reflects the right tables.

### 3. Web UI is a hub-only surface for the four integrations

- Only one Razor page exists: `src/Cardscape.Web/Pages/WorkspaceIntegrations.razor` (the hub).
- The hub's "Configure" buttons for Slack, GitHub, and Email navigate to URLs that do NOT exist as pages.
- The Google Drive button on the hub page routes to `/settings/integrations/google-calendar`, which is the Google **Calendar** page (`src/Cardscape.Web/Pages/GoogleCalendar.razor:1`), not Google **Drive**.
- The plan's "GitHub section in the card menu" (referenced in §3.9) is not implemented.
- No API client services exist for Slack / Google Drive / GitHub / Email (only `GoogleCalendarApiClient.cs`).
- The i18n keys (`SlackBlurb`, `GoogleDriveBlurb`, `GitHubBlurb`, `EmailToBoardBlurb`, `IntegrationsTitle`, `IntegrationsBlurb`) are present in BOTH `SharedResource.resx` and `SharedResource.es.resx` (`SharedResource.resx:121-126`, `SharedResource.es.resx:121-126`) — i18n is DONE for the hub page.

### 4. No integration tests for any of the four features

- `tests/Cardscape.IntegrationTests/` is empty (only `.csproj`, `.editorconfig`, `GlobalUsings.cs`).
- `tests/Cardscape.FunctionalTests/GoldenPathSmokeTests.cs` is the only functional test; it does not exercise the integration paths.
- The four webhooks (card.created, card.moved, card.completed, comment.added) have no test that asserts they fan out to Slack.
- The Google picker flow, GitHub service, and inbound email parser all lack tests.

### 5. Security gap in `POST /api/integrations/email/inbound`

`IntegrationsEndpoints.cs:148` mounts the inbound endpoint WITHOUT `RequireAuthorization()`. The handler (`DefaultInboundEmailService.cs:68-75`) trusts the destination address and creates a card. There is no signature verification (SendGrid `X-Twilio-Email-Event-Webhook-Signature`, Mailgun signing key, Postmark `X-Postmark-Signature`). Any caller who learns a registered `cardscape+board-…@in.example.com` address can create cards in the matching list. The plan called for webhook signature verification ("the inbound-email providers sign the request (the implementation verifies the signature when configured)" — the implementation does NOT, period).

### 6. Dead code: duplicate `Email/InboundEmailAddress.cs` and `Email/InboundEmailAddressId.cs`

`src/Cardscape.Domain/Integrations/Email/` has copies of the inbound email aggregate that no code imports. The actual `InboundEmail` namespace is the one wired up. Either consolidate or delete the `Email/` folder.

---

## Plan checklist updates

The plan was checked for `- [ ]` markers in §3.7–3.10. None of the four items have explicit `- [ ]` checkboxes (the plan uses bullet points, not checkboxes, in §3.7–3.10). The plan has `- [ ]` checkboxes elsewhere (§3.11 OAuth apps, §3.12 OpenAPI, §1.* Hygiene, §2.* MCP, §4.* Enterprise, §5.* i18n / PWA / SDK / etc.). No checkbox changes were made in §3.7–3.10.

If the auditor wants to add a verdict summary at the top of the plan, the suggested block is:

```markdown
### 3.7 Slack integration — PARTIAL (DRIFT). Add DI registration; add `/workspaces/{id}/integrations/slack` page.
### 3.8 Google Drive integration — PARTIAL (DRIFT). Add DI registration; add a dedicated Google Drive page (the hub currently routes to the Google Calendar page).
### 3.9 GitHub integration — PARTIAL (DRIFT). Add DI registration; add `/workspaces/{id}/integrations/github` page + card-menu section.
### 3.10 Email-to-board — PARTIAL (DRIFT). Add DI registration; add `/workspaces/{id}/integrations/email` page; add webhook signature verification (security gap on the public `/api/integrations/email/inbound` endpoint).
```

---

## Final verdicts

| § | Item | Verdict |
|---|------|---------|
| 3.7 | Slack | **PARTIAL** (DRIFT) |
| 3.8 | Google Drive | **PARTIAL** (DRIFT) |
| 3.9 | GitHub | **PARTIAL** (DRIFT) |
| 3.10 | Email-to-board | **PARTIAL** (DRIFT) |

**No item is fully DONE** because every one of the four has the same two gaps:
1. DI registration of its abstractions and repositories is missing — the integrations would crash at runtime.
2. The dedicated per-integration Web UI page that the plan calls for does not exist.

**Most important gap** (in priority order):
1. **DI registration** (CRITICAL, runtime crash) — all 4 items.
2. **Inbound email signature verification** (CRITICAL, security) — §3.10.
3. **Web UI pages** (PARTIAL, broken "Configure" buttons on the hub) — all 4 items.
4. **Migration naming** (DRIFT only, schema is correct) — §3.7 and §3.10.
5. **Integration tests** (gap, but the plan doesn't require them for these items).
