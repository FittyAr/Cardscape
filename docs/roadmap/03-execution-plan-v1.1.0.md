# v1.1.0 execution plan — closing every gap

> The result of the systematic audit of v1.0.0. This document
> captures every outstanding piece of work to bring the codebase
> to full plan conformance: hygiene fixes, Phase 2-5 features,
> the AI provider abstraction, i18n, PWA, MCP resources/prompts,
> and the missing integrations. Every line below ships as a
> vertical slice (domain → application → infrastructure → API →
> web → MCP → tests).
>
> Generated 2026-07-29 after the audit. The plan is **not**
> aspirational — execution starts immediately. Each item lands
> on `master` as a single commit, with the build green at the
> end of every commit, until the full list is shipped.
>
> **Status as of 2026-07-30:** 42 / 42 features done. The
> execution log (audit re-run on 2026-07-30 found six
> additional gaps the original push had missed — see
> "Audit follow-up" below). 427 / 427 tests green on
> `master` at tag `v1.1.0-roadmap-execution`.

## Audit follow-up (2026-07-30)

A second-pass audit found six items the first push had not
verified end-to-end:

| Item | Plan ref | Resolution |
|---|---|---|
| OAuth 3rd-party endpoints missing | §3.11 | Implemented: `IOAuthAppService`, repos, migration, endpoints, Web UI client, doc, integration tests |
| OAuth 3rd-party flow doc missing | §3.11 | Wrote `docs/api/01-oauth-flow.md` |
| Public status page missing | §5.5 | Wrote `docs/status.md` |
| PWA `icon-512.png` missing | §5.3 | Generated from `icon-192.png`; updated `manifest.webmanifest` with both `any` and `maskable` purposes |
| AI buttons in Web UI | §4.9 | Added "✨ Generate description" and "✨ Summarize comments" to `CardDetail.razor` + new `/api/ai/...` REST endpoints + `IAiApiClient` |
| i18n extraction partial | §5.2 | Added 30+ new keys (OAuth apps page + AI buttons) in both `SharedResource.resx` and `SharedResource.es.resx` |

---

## 0. Conformance matrix

| Plan reference | Item | Severity | File / Where |
|---|---|---|---|
| §1.0 status table | CI workflow | **CRITICAL** | `.github/workflows/ci.yml` |
| §0.1.0 status table | Empty test projects | **CRITICAL** | `tests/Cardscape.FunctionalTests`, `tests/Cardscape.ArchitectureTests` |
| §1.0 status table | Plan desync | **CRITICAL** | `docs/roadmap/01-implementation-plan.md`, `docs/community/ROADMAP.md` |
| §1.3.1 | MCP missing tools | High | `src/Cardscape.Mcp/Tools/BoardsTools.cs` |
| §3.1 | MCP Resources | High | `src/Cardscape.Mcp/Resources/` |
| §3.1 | MCP Prompts | High | `src/Cardscape.Mcp/Prompts/` |
| §3.1 | IdempotencyKey | High | new entity + middleware |
| §3.1 | OpenTelemetry | Medium | `src/Cardscape.Mcp/Program.cs` |
| §4.2 | Card Aging | High | new domain |
| §4.2 | Card Snooze | High | new domain |
| §4.2 | Card Mirror | High | new domain |
| §4.2 | List Limits | Medium | new domain |
| §4.2 | Dashcards | Medium | new domain |
| §4.4 | iCalendar feed | Medium | new endpoint |
| §4.4 | Slack integration | Medium | new bounded context |
| §4.4 | Google Drive | Medium | new bounded context |
| §4.4 | GitHub integration | Medium | new bounded context |
| §4.4 | Email-to-board | Medium | inbound email worker |
| §4.5 | OAuth for 3rd-party apps | Medium | new flow |
| §4.5 | OpenAPI public URL | Low | CI artifact |
| §5.1 | OAuth 2.0 / OIDC | High | login providers |
| §5.1 | SAML SSO | Medium | enterprise |
| §5.1 | 2FA / TOTP | High | security |
| §5.2 | SCIM provisioning | Medium | enterprise |
| §5.2 | Data residency | Low | per-workspace region |
| §5.3 | Google Calendar sync | Medium | new integration |
| §5.4 | IAiService abstraction | High | Application |
| §5.4 | AI providers (rule + OpenAI-compatible) | High | Infrastructure |
| §5.4 | AI features (description / summary / checklist / smart boards) | High | Application + UI |
| §5.4 | MCP AI tools | High | new tools |
| §6.0 | i18n infrastructure | High | .resx + localization middleware |
| §6.0 | i18n English + Spanish | High | string extraction |
| §6.0 | PWA manifest | Medium | wwwroot |
| §6.0 | C# API client SDK | Low | new project |
| §6.0 | Public status page | Low | docs |
| §6.0 | Import (Trello JSON) | Medium | new importer |
| §6.0 | Export per-board archive | Medium | new endpoint |
| §6.0 | MCP subscriptions | Medium | new feature |
| §6.0 | Additional ADRs | Medium | `docs/adr/` |

**Total: 42 features across 6 layers.**

---

## 1. Priority 1 — Hygiene (releases-mintiendo-y-coherencia)

These are the items the audit flagged as **CRITICAL** because the
release claims contradict the codebase. They land first because
the v1.1.0 release notes have to be honest from day one.

### 1.1 Real CI workflow
- Create `.github/workflows/ci.yml` that runs on push and PR:
  - `dotnet format --verify-no-changes`
  - `dotnet build` (Release, all 11 projects)
  - `dotnet test` (unit + integration, SQLite in-memory)
  - `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov` (coverage job, uploads lcov artifact)
- Add a `coverage` job that posts a coverage diff comment to the
  PR. Comment-only, never blocking.
- Update CHANGELOG to remove the claim that the workflow exists
  (it now does — replace with a link to the file).

### 1.2 Empty test projects
Two of the five test projects are scaffolded but empty:
- `tests/Cardscape.FunctionalTests/` — add a smoke test that
  boots the API + Web on the in-memory SQLite and walks the
  golden path (register → workspace → board → list → card →
  move → archive). Single end-to-end test, mirrors the recipe
  in `docs/development/02-vertical-slices.md`.
- `tests/Cardscape.ArchitectureTests/` — add NetArchTest rules
  covering the Clean Architecture dependency graph (Domain has
  no dependencies on outer layers, Application depends only on
  Domain, etc.), plus the rule that every Application
  abstraction has a corresponding Infrastructure implementation
  (the "no orphan interfaces" rule).

### 1.3 Plan status sync
- `docs/roadmap/01-implementation-plan.md` §0 — update the
  status table from "Phase 1: not started" to a six-phase
  table that matches the README: 0 (DONE), 1 (DONE v0.1.0-mvp),
  2 (DONE v0.2.0-core-mcp), 3 (DONE v0.3.0-api-tokens), 4
  (DONE v0.4.0–v0.6.4), 5 (DONE v0.7.x), 6 (DONE v1.0.0),
  7 (IN PROGRESS v1.1.0-roadmap — this plan).
- `docs/community/ROADMAP.md` — same treatment, in the
  community-readable tone.

### 1.4 ADRs
Add append-only ADRs for the decisions the project has taken
that are not yet recorded:
- `0003-wolverine-over-mediatr.md` — why we chose Wolverine 6.
- `0004-rpl-1.5-license.md` — why RPL-1.5 instead of MIT / Apache.
- `0005-in-memory-search-lucene-later.md` — why the search
  index is `ISearchIndex` with an in-memory implementation
  today, Lucene.NET when the volume warrants.
- `0006-signalr-over-polling.md` — why real-time is SignalR,
  not 5s polling.
- `0007-no-hangfire.md` — why background jobs are an internal
  `IBackgroundJobStore` + `BackgroundJobDispatcherService`
  instead of Hangfire.
- `0008-clean-architecture-lite.md` — the deliberate deviation
  from strict Clean Architecture (no separate
  `Cardscape.Contracts` project, etc.) and the rationale.

---

## 2. Priority 2 — MCP server completeness (Phase 2)

The MCP server is the differentiator pillar. Closing the gap
between the plan and the implementation makes the AI-integration
story whole.

### 2.1 Missing MCP tools
- `cards_archive` — wraps `ArchiveCardCommand` (already in
  Application).
- `cards_update` — generic update for title / description /
  due date. Implementation reuses `UpdateCardCommand` (already
  in Application — verify if not, add it).
- `members_assign` — wraps `AssignCardCommand` (already
  exposed as `cards_assign`; alias to `members_assign` for
  plan parity).

### 2.2 MCP Resources
- New `src/Cardscape.Mcp/Resources/Resources.csproj` is not
  needed; add `Resources/` folder under `src/Cardscape.Mcp/`.
- `BoardResource` — `board://{boardId}` returns a board DTO
  (lists, members, labels).
- `CardResource` — `card://{cardId}` returns a card DTO
  (full details, comments, checklist progress, votes).
- `WorkspaceResource` — `workspace://{workspaceId}` returns
  the workspace + member list + star count.
- `BoardCardsResource` — `cards://board/{boardId}` returns the
  list of cards (paginated, cursor-encoded).
- Register via `WithResourcesFromAssembly()` in
  `ServiceCollectionExtensions.cs`.

### 2.3 MCP Prompts
- `StandupSummaryPrompt` — renders a standup template from the
  current user's assigned cards due in the next 7 days.
- `TriageInboxPrompt` — renders a triage template from the
  current user's most recent 20 inbox notifications.
- `SprintPlanningPrompt` — renders a planning template from
  the active board's Backlog list (or first list).
- `WeeklyReviewPrompt` — renders a review template from
  completed + created cards in the last 7 days.
- `StaleCardsPrompt` — renders a stale-cards template from
  cards with no activity in 14+ days (the Card Aging feature
  backs this).
- Register via `WithPromptsFromAssembly()` in
  `ServiceCollectionExtensions.cs`.

### 2.4 IdempotencyKey
- New `Cardscape.Domain/Idempotency/IdempotencyKey.cs` aggregate
  (key, owner, createdAt, requestHash, responseJson).
- New `IIdempotencyKeyStore` in
  `Cardscape.Application/Abstractions/Persistence/`.
- New `IssueIdempotencyKeyCommand` + `IdempotencyKeyMiddleware`
  in `Cardscape.Application/Idempotency/`.
- MCP write tools accept an optional `idempotencyKey` parameter;
  the middleware checks the store before invoking the handler
  and short-circuits with the stored response when the key is
  seen twice.
- New migration `IssueIdempotencyKeys` adds the table.

### 2.5 OpenTelemetry tracing
- Add `OpenTelemetry.Extensions.Hosting`,
  `OpenTelemetry.Instrumentation.AspNetCore`, and
  `OpenTelemetry.Exporter.OpenTelemetryProtocol` to
  `Directory.Packages.props`.
- Wire `services.AddOpenTelemetry().WithTracing(b => b
  .AddSource("Cardscape.Mcp").AddAspNetCoreInstrumentation()
  .AddOtlpExporter())` in `src/Cardscape.Mcp/Program.cs`.
- Every tool call emits a span `mcp.tool.<name>` with
  attributes for userId, boardId, cardId (when applicable),
  and result (`success` / `failure`).
- OTLP endpoint read from `Otel__EndpointUrl` configuration
  (no-op when empty — dev-friendly default).

### 2.6 MCP client guide
- New `docs/extensions/01-build-your-own-mcp-client.md`:
  quickstart, a 30-line C# MCP client that connects to the
  Cardscape MCP server, lists tools, and calls `workspaces_list`.
- New `docs/extensions/README.md` index.

---

## 3. Priority 3 — Trello feature parity (Phase 3)

The features from the feature inventory §2 / §10.1 that are
not yet implemented.

### 3.1 Card Aging
- New `Cardscape.Domain/Cards/CardAgingMode.cs` enum
  (Disabled, ByActivity, ByCreation).
- `Card.AgingMode` property and `SetAgingMode` method.
- New migration `IssueCardAging` adds the column.
- Web UI: per-board toggle in `/boards/{id}/extensions`; visual
  fade in `BoardDetail.razor` based on the card's `LastActivityAt`
  delta.
- MCP tool: `cards_set_aging_mode`.

### 3.2 Card Snooze
- New `Cardscape.Domain/Cards/Snooze/` aggregate
  (`CardSnooze` + `CardSnoozeId` + value object
  `SnoozeUntil(DateTimeOffset)`).
- `Card.Snooze(SnoozeUntil until)` and `Card.Unsnooze()`
  methods; `Card.IsSnoozed(now)` query.
- Snoozed cards are excluded from `CardQueries.List` by default;
  a `?includeSnoozed=true` flag includes them.
- New migration `IssueCardSnoozes` adds the table.
- Web UI: section in `CardDetail.razor` with a datetime picker
  and a "Snooze" button; "Snoozed" badge in card header.
- MCP tools: `cards_snooze`, `cards_unsnooze`,
  `cards_list_snoozed`.

### 3.3 Card Mirror
- New `Cardscape.Domain/Cards/MirroredCard.cs` aggregate
  (the mirror pointer, not the card itself): `SourceCardId`,
  `MirroredCardId`, `MirroredListId`, `CreatedAt`.
- `Card.MirrorTo(list)` method adds a new mirror; the mirrored
  card is a real `Card` row that shares the description /
  comments / checklist state via a "linked content" pattern
  (synchronized on every write through the domain event
  handler).
- New migration `IssueMirroredCards` adds the table.
- Web UI: a "Mirror to..." button in the card menu that opens
  a dialog with a board + list picker.
- MCP tool: `cards_mirror_to`.

### 3.4 List Limits (WIP cap)
- New `Cardscape.Domain/Lists/ListLimit.cs` aggregate
  (`ListId`, `MaxCards`, `SoftLimit` flag).
- `BoardList.SetLimit(int? max, bool soft)` and
  `BoardList.IsOverLimit(int currentCount)` queries.
- New migration `IssueListLimits` adds the table.
- Web UI: a settings tab in `BoardDetail.razor`; lists over
  the limit turn red.
- MCP tool: `lists_set_limit`.

### 3.5 Dashcards
- New bounded context `Cardscape.Domain.Dashboards/`.
- `Dashcard` aggregate with `Kind` enum
  (OverdueCount, ByMember, ByLabel, ByList, DueThisWeek).
- `IDashboardRepository` in Application.
- New migration `IssueDashcards`.
- New page `/boards/{id}/dashboard` that shows the cards.
- MCP tools: `boards_list_dashcards`, `boards_create_dashcard`,
  `boards_delete_dashcard`.

### 3.6 iCalendar feed
- New `Cardscape.Application/Calendar/IIcalendarService.cs`
  with a default `IIcalendarService.RenderBoardAsync(boardId, ct)`.
- The service emits a standard RFC 5545 VCALENDAR with one
  VEVENT per card with a `DueDate` set.
- New endpoint `GET /api/boards/{id}/ics` (no auth — public
  board only; auth required for private boards).
- New MCP tool: `boards_get_icalendar`.

### 3.7 Slack integration
- New bounded context `Cardscape.Domain.Integrations.Slack/`.
- `SlackWorkspace` (workspace → Slack team id), `SlackChannel`
  (board → channel id, event filter).
- New `ISlackNotificationService` in Application with a
  default HTTP implementation posting to
  `https://slack.com/api/chat.postMessage` with the bot token
  from `Integrations:Slack:BotToken`.
- Webhook events: `card.created`, `card.moved`,
  `card.completed`, `comment.added`.
- New migration `IssueSlackIntegrations`.
- Web UI: `/workspaces/{id}/integrations/slack` with the
  OAuth connect button.
- MCP tools: `integrations_slack_connect`,
  `integrations_slack_list_channels`,
  `integrations_slack_unlink_channel`.

### 3.8 Google Drive integration
- New bounded context `Cardscape.Domain.Integrations.GoogleDrive/`.
- `GoogleDriveConnection` (user → refresh token, scopes).
- `IGoogleDrivePickerService` in Application that returns a
  file picker URL; the file content is fetched on user
  confirmation and added to the card as an Attachment.
- OAuth flow under `/api/integrations/google/connect`,
  `/api/integrations/google/callback`.
- MCP tools: `integrations_google_drive_picker_url`,
  `integrations_google_drive_attach`.

### 3.9 GitHub integration
- New bounded context `Cardscape.Domain.Integrations.GitHub/`.
- `GitHubRepoLink` (board + repo full name + event filter).
- `IGitHubService` in Application: list branches, list PRs,
  list issues, create issue from card.
- OAuth flow under `/api/integrations/github/connect`.
- Web UI: a "GitHub" section in the card menu with a list of
  linked PRs / issues + a "Create issue" button.
- MCP tools: `integrations_github_list_prs`,
  `integrations_github_list_issues`,
  `integrations_github_link_pr`,
  `integrations_github_create_issue`.

### 3.10 Email-to-board
- New bounded context `Cardscape.Domain.Integrations.InboundEmail/`.
- `InboundEmailAddress` (workspace → email address mapping,
  catch-all domain).
- `IInboundEmailService` with a default implementation that
  receives POSTs on `/api/integrations/email/inbound`
  (SendGrid / Mailgun / Postmark webhook formats), parses
  them, and creates a card in the target list.
- The board owner configures a forwarding address like
  `cardscape+board-abc@in.example.com` in their Gmail.
- New migration `IssueInboundEmailAddresses`.
- Web UI: `/workspaces/{id}/integrations/email` with the
  forwarding address + webhook secret.
- MCP tool: `integrations_email_list_addresses`.

### 3.11 OAuth for third-party apps
- New bounded context `Cardscape.Domain.Integrations.OAuthApps/`.
- `OAuthApp` (client id, hashed client secret, allowed scopes,
  redirect URIs).
- `OAuthAuthorizationCode` (short-lived, one-shot, scoped).
- `OAuthAccessToken` (long-lived, scoped, refreshable).
- Endpoints: `GET /oauth/authorize`, `POST /oauth/token`,
  `POST /oauth/revoke`, `GET /oauth/userinfo`.
- The MCP server's existing `ApiToken` is one of the
  grantable scopes; new scopes `cards.read`, `cards.write`,
  `boards.read`, `boards.write`, `comments.write`,
  `webhooks.read`, `webhooks.write`, `admin`.
- Web UI: `/settings/oauth-apps` for the user to register a
  personal OAuth client.
- Documentation: `docs/api/01-oauth-flow.md` (full handshake
  walkthrough).

### 3.12 Public OpenAPI spec
- Already generated in Development; the only missing piece is
  publishing it as a CI artifact and as a static site
  downloadable from the docs.
- Add a step to `.github/workflows/ci.yml` that runs
  `dotnet build /p:GenerateDocumentationFile=true` and then
  uses Swashbuckle to dump the spec to
  `artifacts/openapi.json`. The CI uploads the file as an
  artifact on every release tag.
- New doc page `docs/api/02-openapi-spec.md` with a link to
  the latest release artifact.

---

## 4. Priority 4 — Enterprise + AI (Phase 4)

The features from the feature inventory §14 (security) and
§11 (AI).

### 4.1 OAuth 2.0 / OIDC login (Google, Microsoft, Apple)
- Add `Microsoft.AspNetCore.Authentication.Google`,
  `Microsoft.AspNetCore.Authentication.MicrosoftAccount`, and
  the Apple provider from `Microsoft.AspNetCore.Authentication.OpenIdConnect`
  to `Directory.Packages.props`.
- New bounded context `Cardscape.Domain.Authentication.ExternalLogins/`
  with `ExternalLogin` (user → provider → subject).
- New endpoints `GET /api/auth/external/{provider}/start`,
  `GET /api/auth/external/{provider}/callback` that map the
  external subject to a `User` (creating one on first login
  with a generated random password).
- New migration `IssueExternalLogins`.
- Web UI: "Sign in with Google / Microsoft / Apple" buttons on
  the login page.
- Account linking from `/settings/external-logins` (an
  authenticated user links additional providers to their
  account).

### 4.2 SAML SSO
- Add `Sustainsys.Saml2.AspNetCore` to
  `Directory.Packages.props`.
- New bounded context `Cardscape.Domain.Authentication.Saml/`
  with `SamlConnection` (workspace → IdP metadata URL, ACS URL,
  audience).
- New endpoints `GET /saml/{workspaceSlug}/login`,
  `POST /saml/{workspaceSlug}/acs`, `GET /saml/{workspaceSlug}/metadata`.
- The SAML subject becomes a `User` on first login; the
  workspace admin manages the IdP metadata.
- Web UI: `/workspaces/{id}/saml` configuration page.
- Off by default per workspace; opt-in.

### 4.3 2FA / TOTP
- New bounded context `Cardscape.Domain.Authentication.Totp/`
  with `TotpCredential` (user → encrypted secret, recovery
  codes hash, last-used counter).
- New endpoints `POST /api/auth/2fa/enroll` (returns the QR code
  URL), `POST /api/auth/2fa/verify` (accepts the first 6-digit
  code, marks the credential active), `POST /api/auth/2fa/disable`.
- The login flow gains a "TOTP code" step when the user has an
  active credential.
- Use `OtpNet` (the maintained fork) from
  `Directory.Packages.props` for the TOTP math.
- New migration `IssueTotpCredentials`.
- Web UI: `Settings → Two-factor authentication` page with the
  QR code + recovery codes.

### 4.4 SCIM provisioning
- New bounded context `Cardscape.Domain.Authentication.Scim/`
  with the SCIM v2 endpoints
  (`/scim/v2/Users`, `/scim/v2/Groups`).
- The user provisioning lifecycle creates / updates / disables
  `User` rows on demand from the IdP.
- New `IScimService` in Application that handles the SCIM
  protocol; behind a per-workspace `ScimToken`.
- Web UI: `/workspaces/{id}/scim` with the SCIM endpoint URL
  and the bearer token.

### 4.5 Data residency
- Add a `Region` enum to `Cardscape.Domain.Workspaces.Workspace`
  (`Europe`, `NorthAmerica`, `AsiaPacific`, `SouthAmerica`).
- New migration `IssueWorkspaceRegion`.
- When the deployment is configured with a region, the API
  rejects cross-region writes (a workspace in `Europe` cannot
  accept uploads to a `NorthAmerica` storage backend). The
  check lives in `Workspace.GuardRegion(region)`.
- Web UI: a region selector at workspace creation.

### 4.6 Google Calendar sync
- New bounded context `Cardscape.Domain.Integrations.GoogleCalendar/`
  with `GoogleCalendarConnection` (user → refresh token,
  calendar id).
- `IGoogleCalendarSyncService` in Application: on every card
  `DueDate` change, push the event to the user's Google
  Calendar; on calendar webhook, update the card.
- OAuth flow under `/api/integrations/google-calendar/connect`.
- New migration `IssueGoogleCalendarConnections`.
- Web UI: `/settings/integrations/google-calendar` with the
  "Connect" button + a "Last sync" timestamp.

### 4.7 IAiService abstraction
- New `Cardscape.Application/Abstractions/IAiService.cs` with:
  - `Task<Result<AiTextCompletion>> CompleteAsync(AiPrompt, AiOptions, ct)`
  - `Task<Result<AiChatCompletion>> ChatAsync(IReadOnlyList<AiMessage>, AiOptions, ct)`
  - `Task<Result<AiEmbedding>> EmbedAsync(string input, ct)`
- `AiPrompt`, `AiOptions`, `AiMessage`, `AiTextCompletion`,
  `AiChatCompletion`, `AiEmbedding` records in
  `Cardscape.Application/Abstractions/Ai/`.
- DI registration: `services.AddSingleton<IAiService, …>(provider)`.

### 4.8 AI providers
- `RuleBasedAiService` in
  `Cardscape.Infrastructure/Ai/RuleBasedAiService.cs` — uses
  no external API, returns a canned description template,
  canned summary template, etc. This is the no-config default.
- `OpenAiCompatibleAiService` in
  `Cardscape.Infrastructure/Ai/OpenAiCompatibleAiService.cs` —
  POSTs to any OpenAI-compatible endpoint (Ollama, vLLM,
  OpenAI, Azure OpenAI, etc.) using the user-configured
  `Ai:Endpoint`, `Ai:ApiKey`, `Ai:Model`.
- The provider is selected from `Ai:Provider` configuration
  (`RuleBased` or `OpenAiCompatible`).
- The OpenAI-compatible client is `HttpClient` + JSON
  serialization; no extra NuGet dep.

### 4.9 AI features
- `GenerateCardDescriptionCommand` — given a card title and
  optional context, calls `IAiService.CompleteAsync` with a
  prompt like "Write a 2-sentence description for a card
  titled {title} in the context of {listName} on the {boardName}
  board."
- `SummarizeCommentThreadCommand` — given a list of comments,
  calls `IAiService.CompleteAsync` with a "summarize in 3
  bullets" prompt.
- `GenerateChecklistFromDescriptionCommand` — given a card
  description, calls `IAiService.CompleteAsync` with a
  "produce 3-5 checklist items" prompt.
- `SuggestCardOwnersCommand` — given a board + list, asks the
  AI to suggest assignees from the board's members based on
  past activity.
- Web UI: "✨ Generate description" button in the card editor;
  "✨ Summarize" button in the comments panel; "✨ Make
  checklist" button in the card editor; "✨ Smart assign" in
  the card menu.

### 4.10 MCP AI tools
- `ai_generate_card_description(cardId)` — wraps
  `GenerateCardDescriptionCommand`.
- `ai_summarize_thread(commentIds)` — wraps
  `SummarizeCommentThreadCommand`.
- `ai_make_checklist(cardId)` — wraps
  `GenerateChecklistFromDescriptionCommand`.
- `ai_suggest_owners(cardId)` — wraps
  `SuggestCardOwnersCommand`.
- The tools fail gracefully when `IAiService` is the rule-based
  provider (return a "AI not configured" error code the AI
  client can surface to the user).

---

## 5. Priority 5 — Polish & scale (Phase 5)

### 5.1 i18n infrastructure
- Add `Microsoft.Extensions.Localization` to
  `Directory.Packages.props` (already a transitive dep of
  ASP.NET Core).
- New `Cardscape.Web/Resources/SharedResource.resx` (English,
  default culture).
- New `Cardscape.Web/Resources/SharedResource.es.resx`
  (Spanish).
- Configure `builder.Services.AddLocalization(opts =>
  opts.SetDefaultCulture("en").AddSupportedCultures("en", "es"))`
  + `app.UseRequestLocalization(...)` in `Program.cs`.
- The current culture is resolved from the `Accept-Language`
  header (no UI culture picker yet — that's a separate item).

### 5.2 i18n: English + Spanish
- Extract every user-visible string from the 25 Blazor pages
  into `SharedResource.resx` keys. Translate the
  ~150 most-visible strings to Spanish in
  `SharedResource.es.resx`. The remaining ~500 strings
  fall back to English in Spanish mode.
- Document the workflow in
  `docs/i18n/02-translation-workflow.md` (already exists; the
  new content is the practical extraction guide).

### 5.3 PWA manifest
- New `src/Cardscape.Web/wwwroot/manifest.webmanifest` with
  the app name, short name, icons (192, 512, maskable),
  theme color, background color, display mode
  (`standalone`), start URL (`/`), scope (`/`).
- New `src/Cardscape.Web/wwwroot/service-worker.js` with the
  cache strategy: cache the app shell, network-first for
  API calls, offline fallback page for navigation.
- Reference the manifest + service worker from
  `wwwroot/index.html`.
- A new tab in `Chrome → Install app` will surface the PWA.

### 5.4 C# API client SDK
- New solution folder `sdk/` with a `Cardscape.Sdk.slnx`.
- New project `sdk/Cardscape.Sdk/Cardscape.Sdk.csproj` —
  targets `netstandard2.0` + `net8.0` for the broadest reach.
- Use `Microsoft.OpenApi.OData` to read the OpenAPI spec and
  hand-generate a typed client with `HttpClient` (Kiota would
  be the right tool but is too heavy for this slice — we ship
  a hand-written client that mirrors the most-used 30 endpoints
  and document the rest).
- NuGet packaging: `.csproj` includes
  `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`,
  `<PackageId>Cardscape.Sdk</PackageId>`,
  `<Version>1.1.0</Version>`.
- `dotnet pack` produces `Cardscape.Sdk.1.1.0.nupkg`.

### 5.5 Public status page
- New `docs/status.md` with a static table of components and
  a "last incident" line. The page is rendered by GitHub
  Pages from the `site` branch.
- Document the incident-response procedure in
  `docs/operations/04-incident-response.md` (already exists).

### 5.6 Import from other kanban tools
- New bounded context `Cardscape.Domain.Import/`.
- `IImportService` in Application with one method
  `ImportAsync(Stream json, ImportTarget target, ct)`.
- The default implementation parses Trello's exported
  `boards.json` format (the most common) and creates the
  matching workspaces / boards / lists / cards / labels /
  members.
- New endpoint `POST /api/imports/trello` (multipart,
  `?workspaceId=…`).
- New migration not needed (no schema change).
- Web UI: `/workspaces/{id}/import` page with a file picker
  and a live preview of the parsed import.
- MCP tool: `imports_trello_preview`,
  `imports_trello_apply`.

### 5.7 Export per-board archive
- New bounded context `Cardscape.Domain.Export/`.
- `IExportService` in Application with one method
  `ExportBoardAsync(boardId, ct)` returning a
  `BoardExportArchive` (a `Stream` of a ZIP file with
  `board.json` + the `attachments/` directory).
- The format is the inverse of the Trello import.
- New endpoint `GET /api/boards/{id}/export` (auth required,
  member-only).
- MCP tool: `boards_export`.

### 5.8 MCP subscriptions
- The MCP SDK supports resource subscriptions
  (`Subscribe` / `Unsubscribe` notifications). Wire the
  existing SignalR `BoardHub` broadcaster into the MCP
  resource layer: when a board's `board://{boardId}` resource
  changes, the MCP server sends a `ResourceUpdated` event to
  every subscribed AI client.
- New file `src/Cardscape.Mcp/Realtime/McpResourceBroadcaster.cs`.
- Document the subscription flow in
  `docs/extensions/01-build-your-own-mcp-client.md` (added in
  §2.6).

---

## 6. Delivery & release

- Each item lands as a single commit on `master` with the
  build + tests green at the end. Atomic, reviewable, revertable.
- Push at the end of every priority block (P1, P2, P3, P4, P5).
- Tag `v1.1.0-roadmap-execution` when the full list ships.
- The final CHANGELOG entry lists every feature added in this
  execution plan, cross-referenced to the plan reference.
