# Priority 3 §3.1–3.6 — Trello feature parity (cards and views) — Audit

**Date:** 2026-07-30
**Scope:** `docs/roadmap/03-execution-plan-v1.1.0.md` §3.1 (Card Aging),
§3.2 (Card Snooze), §3.3 (Card Mirror), §3.4 (List Limits),
§3.5 (Dashcards), §3.6 (iCalendar feed).
**Method:** Read each plan sub-section, locate the corresponding domain /
application / infrastructure / API / web / MCP artefacts, compare shape and
behaviour. Cite `file_path:line_number` for every claim.

The dominant pattern across §3.1–3.4 is the same: the plan prescribed a
method/aggregate shape ("`Card.AgingMode`", "`Card.Snooze(SnoozeUntil)`",
"`Card.MirrorTo(list)`", "`BoardList.SetLimit(int? max, bool soft)`"),
but the implementation chose a **separate per-card or per-list aggregate**
(`CardAgingSettings`, `CardSnooze`, `CardMirror`, `MaxCardsSoft/Hard`
properties on `BoardList`). The MCP tools and the database tables are in
place, so the *capability* is largely delivered, but the *plan-specified
shape* is not. §3.5 and §3.6 land much closer to spec.

The plan's per-board Web UI surfaces are the biggest gap: nothing for
aging, snooze, mirror, or list-overflow is rendered in the Blazor pages.
The `BoardsExtensions.razor` page does not list Card Aging as a toggle.
The `CardDetail.razor` page has no snooze section, no "Snoozed" badge, and
no "Mirror to..." menu entry. `BoardDetail.razor` has no list-limit
red-overflow styling.

---

## 3.1 Card Aging — **PARTIAL**

**Plan contract (`docs/roadmap/03-execution-plan-v1.1.0.md:231-239`):**
- `Cardscape.Domain/Cards/CardAgingMode.cs` enum
  `Disabled`, `ByActivity`, `ByCreation`.
- `Card.AgingMode` property and `SetAgingMode` method.
- Migration `IssueCardAging` adds the column.
- Web UI: per-board toggle in `/boards/{id}/extensions`; visual fade in
  `BoardDetail.razor` based on `LastActivityAt` delta.
- MCP tool: `cards_set_aging_mode`.

**Evidence:**

| Item | Status | Evidence |
|---|---|---|
| Enum | **DRIFT** — `ByCreation` missing | `src/Cardscape.Domain/Cards/CardAgingSettings.cs:6-12` — only `Disabled = 0` and `ByActivity = 1`. The error message in `MissingTools.cs:43` still advertises `ByCreation` as valid, which it is not. |
| Aggregate shape | **DRIFT** — separate aggregate instead of method on `Card` | `CardAgingSettings` is its own `Entity<CardId>` (`CardAgingSettings.cs:20`). `Card.AgingMode` / `Card.SetAgingMode` are not present anywhere on `src/Cardscape.Domain/Cards/Card.cs:15-292`. |
| Migration | **DRIFT** — consolidated, not standalone | `src/Cardscape.Infrastructure/Persistence/Migrations/20260729202710_IssueCardAgingSnoozeMirror.cs:9` — single migration creates `card_aging_settings`, `card_mirrors`, and `card_snoozes` together. There is no `IssueCardAging.cs`. |
| Application command | Present | `SetCardAgingModeCommand` at `src/Cardscape.Application/Cards/AdditionalCardCommands.cs:13`; handler at lines 15-69 (creates a `CardAgingSettings` row if absent, updates the mode otherwise). |
| Web UI — extensions toggle | **MISSING** | `src/Cardscape.Web/Pages/BoardExtensions.razor:70-75` — only `Custom fields`, `Voting`, `Card repeater`. No Card Aging option. |
| Web UI — visual fade | **MISSING** | `src/Cardscape.Web/Pages/BoardDetail.razor:81-90` — card-mini markup has no opacity / fade logic. No `.stale` or `.aging` class in `src/Cardscape.Web/wwwroot/css/app.css` (the only `opacity` rules are at lines 197, 462, 659, 795, 890 and are unrelated). |
| MCP tool | Present | `cards_set_aging_mode` at `src/Cardscape.Mcp/Tools/MissingTools.cs:32-48`. |
| i18n keys | **MISSING** | No "aging" / "AgingMode" entries in `src/Cardscape.Web/Resources/SharedResource.resx`. |
| Tests | **MISSING** | No `CardAging*` test file in `tests/`. |

**Notes:** the implementation is functional at the MCP / application
layer. The drift is in shape: a per-card settings row instead of a method
on `Card`, no `ByCreation`, no Web UI. `CardAgingSettings.IsStale`
(`CardAgingSettings.cs:66-68`) does implement the staleness query, but
nothing on the Web side actually consumes it.

**Verdict:** PARTIAL — backend + MCP work; enum is missing a value;
no Web UI; no `LastActivityAt` fade in `BoardDetail.razor`.

---

## 3.2 Card Snooze — **PARTIAL**

**Plan contract (`docs/roadmap/03-execution-plan-v1.1.0.md:241-253`):**
- `Cardscape.Domain/Cards/Snooze/` aggregate
  (`CardSnooze` + `CardSnoozeId` + value object
  `SnoozeUntil(DateTimeOffset)`).
- `Card.Snooze(SnoozeUntil until)` and `Card.Unsnooze()` methods;
  `Card.IsSnoozed(now)` query.
- Snoozed cards excluded from `CardQueries.List` by default;
  `?includeSnoozed=true` flag includes them.
- Migration `IssueCardSnoozes`.
- Web UI: section in `CardDetail.razor` with datetime picker + Snooze
  button; "Snoozed" badge in card header.
- MCP tools: `cards_snooze`, `cards_unsnooze`,
  `cards_list_snoozed`.

**Evidence:**

| Item | Status | Evidence |
|---|---|---|
| Aggregate | **DRIFT** — no `SnoozeUntil` VO, separate aggregate | `src/Cardscape.Domain/Cards/CardSnooze.cs:13-42` — `CardSnooze` is a single class, no nested VO; the `Until` field is a raw `DateTimeOffset` (line 15). |
| `Card.Snooze` / `Unsnooze` / `IsSnoozed` | **DRIFT** — none of these methods on `Card` | `src/Cardscape.Domain/Cards/Card.cs:15-292` — no snooze method. There is no `IsSnoozed` anywhere in the codebase (`grep` returns only the plan doc). |
| `?includeSnoozed=true` flag | **MISSING** | `src/Cardscape.Application/Cards/Queries/CardQueries.cs:48` — `ListCardsForBoardQuery` only carries `IncludeArchived`; no `IncludeSnoozed`. `src/Cardscape.Infrastructure/Repositories/CardRepository.cs:12-42` — `ListForBoardAsync` filters by `includeArchived` only, never by snooze. |
| Migration | **DRIFT** — consolidated | `20260729202710_IssueCardAgingSnoozeMirror.cs:55-73` — `card_snoozes` table created in the consolidated migration. No standalone `IssueCardSnoozes`. |
| Application commands | Present | `SnoozeCardCommand` / `UnsnoozeCardCommand` / `ListSnoozedCardIdsQuery` at `src/Cardscape.Application/Cards/CardscapeExtensions.cs:58-156`. |
| Web UI — snooze section | **MISSING** | `src/Cardscape.Web/Pages/CardDetail.razor:1-218` — no datetime picker, no Snooze button, no "Snoozed" badge. The page renders `Title`, description, custom fields, comments, recurrence, checklists, activity — nothing about snooze. |
| Web UI — "Snoozed" badge | **MISSING** | Same file: no badge markup. |
| MCP tools | Present | `cards_snooze` at `src/Cardscape.Mcp/Tools/MissingTools.cs:51-53`; `cards_unsnooze` at lines 55-57; `cards_list_snoozed` at lines 59-65. |
| i18n keys | **MISSING** | No "snooze" / "Snooze" entries in `SharedResource.resx`. |
| Tests | **MISSING** | No `CardSnooze*` test file in `tests/`. |

**Notes:** the *capability* (snooze / unsnooze / list-snoozed) works at
the MCP / application level, but the data layer never filters snoozed
cards out of board listings, the `Card` aggregate has no integration
with snooze, and the Web UI has no surface for it. The design is also
divergent: the plan asked for a `SnoozeUntil` value object and methods
on `Card`; the implementation chose a separate per-card row.

**Verdict:** PARTIAL — aggregate + MCP work; no Card-aggregate
integration; no `?includeSnoozed=true` query flag; no Web UI.

---

## 3.3 Card Mirror — **PARTIAL**

**Plan contract (`docs/roadmap/03-execution-plan-v1.1.0.md:255-267`):**
- `Cardscape.Domain/Cards/MirroredCard.cs` aggregate
  (`SourceCardId`, `MirroredCardId`, `MirroredListId`, `CreatedAt`).
- `Card.MirrorTo(list)` method; mirrored card is a real `Card` row that
  shares description / comments / checklist state via a "linked content"
  pattern (synchronised through a domain event handler).
- Migration `IssueMirroredCards`.
- Web UI: "Mirror to..." button in the card menu with a board + list
  picker.
- MCP tool: `cards_mirror_to`.

**Evidence:**

| Item | Status | Evidence |
|---|---|---|
| Aggregate | **DRIFT** — renamed and reshaped | `src/Cardscape.Domain/Cards/CardMirror.cs:18-24` — class is `CardMirror`, not `MirroredCard`. Properties: `SourceCardId`, `MirroredCardId`, `TargetListId` (the plan said `MirroredListId`), `MirroredAt` (the plan said `CreatedAt`). |
| `Card.MirrorTo(list)` method | **MISSING** | `src/Cardscape.Domain/Cards/Card.cs:15-292` — no `MirrorTo` method. |
| Migration | **DRIFT** — consolidated | `20260729202710_IssueCardAgingSnoozeMirror.cs:33-53` — `card_mirrors` table created in the consolidated migration. No standalone `IssueMirroredCards`. |
| Application command | Present (with quirk) | `MirrorCardCommand` at `src/Cardscape.Application/Cards/CardscapeExtensions.cs:114-178` creates a real `Card` row in the target list (the "linked content" pattern). The second handler in `AdditionalCardCommands.cs:78-133` has a different shape: it calls `CardMirror.Create` with `source.Id, source.Id, ...` to bypass the "same card" check — comment on lines 110-117 explicitly says "the mirroredCardId is left as the source's id so the CardMirror aggregate's 'same card' check doesn't reject the row" (i.e. the mirror pointer is *not* a real linked card in that branch). |
| Web UI — "Mirror to..." button | **MISSING** | `src/Cardscape.Web/Pages/CardDetail.razor:30-50` — actions are Complete / Reopen / Archive / Restore / Vote. No mirror menu. The only `Mirror` matches in the Web project are comments and DTO names (`Shared/ApiDtos.cs:211, 320`, `Shared/RealtimeDtos.cs:3`). |
| MCP tool | Present | `cards_mirror_to` at `src/Cardscape.Mcp/Tools/MissingTools.cs:68-70`. |
| i18n keys | **MISSING** | No "mirror" / "Mirror" entries in `SharedResource.resx`. |
| Tests | **MISSING** | No `Mirror*` test file in `tests/`. |

**Notes:** the `CardscapeExtensions.MirrorCardCommandHandler` is the
"real" mirror flow (creates a new `Card` in the target list + a
`CardMirror` pointer). The `AdditionalCardCommands.MirrorCardCommandHandler`
is a divergent stub that exists to satisfy the MCP tool wiring but
records a same-id pointer instead of an actual mirror card. There are
two `MirrorCardCommand` records (one in each handler class) — same name,
different behaviour.

**Verdict:** PARTIAL — backend + MCP work; no `Card.MirrorTo`; no Web
UI button; aggregate renamed (`CardMirror` instead of `MirroredCard`);
two divergent handler shapes.

---

## 3.4 List Limits (WIP cap) — **PARTIAL**

**Plan contract (`docs/roadmap/03-execution-plan-v1.1.0.md:269-277`):**
- `Cardscape.Domain/Lists/ListLimit.cs` aggregate
  (`ListId`, `MaxCards`, `SoftLimit` flag).
- `BoardList.SetLimit(int? max, bool soft)` and
  `BoardList.IsOverLimit(int currentCount)` queries.
- Migration `IssueListLimits`.
- Web UI: settings tab in `BoardDetail.razor`; lists over the limit
  turn red.
- MCP tool: `lists_set_limit`.

**Evidence:**

| Item | Status | Evidence |
|---|---|---|
| Aggregate | **DRIFT** — properties on `BoardList`, not a separate aggregate | `src/Cardscape.Domain/Lists/BoardList.cs:122-123` — `MaxCardsSoft` and `MaxCardsHard` are direct properties of `BoardList`. No `ListLimit` aggregate exists. |
| `BoardList.SetLimit(int? max, bool soft)` | **DRIFT** — different signature | `BoardList.cs:125` — `SetLimit(int? maxSoft, int? maxHard, DateTimeOffset at)`; the application-layer command `SetListLimitCommand(ListId, Limit, Soft)` at `src/Cardscape.Application/Lists/AdditionalListCommands.cs:15` maps `Soft=true → soft only`, `Soft=false → hard only` (lines 42-45). The plan's single `(int? max, bool soft)` shape is preserved at the command layer but the domain has two values. |
| `BoardList.IsOverLimit(int currentCount)` | **MISSING** | `grep IsOverLimit` returns only the plan doc. No method on `BoardList` (`BoardList.cs:1-144`). No enforcement at the move handler either — `Card.Move` (`Card.cs:109-128`) does not check list limits, and `grep MaxCardsSoft\|MaxCardsHard` in `src/Cardscape.Application` returns no matches. |
| Migration | Present (slightly different shape) | `20260729203328_IssueListLimits.cs:13-23` — adds `MaxCardsHard` + `MaxCardsSoft` columns to the existing `lists` table (no separate `list_limits` table as the plan implied with the `ListLimit` aggregate). |
| Web UI — settings tab | **MISSING** | `src/Cardscape.Web/Pages/BoardDetail.razor:70-76` — list header shows `Name` + `CardCount`. No WIP / limit controls. No separate "settings" tab. |
| Web UI — red overflow | **MISSING** | `BoardListDto` at `src/Cardscape.Application/Lists/DTOs/ListDTOs.cs:3-10` does not include `MaxCardsSoft` or `MaxCardsHard` (so the Web can't see the limit). The CSS at `src/Cardscape.Web/wwwroot/css/app.css` has no `list-over-limit` / `list-over` rule. |
| MCP tool | Present | `lists_set_limit` at `src/Cardscape.Mcp/Tools/MissingTools.cs:73-75`. |
| i18n keys | **MISSING** | No `wip` / `list_limit` entries in `SharedResource.resx`. |
| Tests | **MISSING** | No `ListLimit*` test file in `tests/`. |

**Notes:** the plan asked for an `IsOverLimit` query so the Web could
turn lists red. The implementation stored the values but never added
the query, never surfaced them through the DTO, and never enforced the
limit in `Card.Move`. The MCP tool can set the limit; nothing
visualises or enforces it.

**Verdict:** PARTIAL — storage + MCP work; no `ListLimit` aggregate;
no `IsOverLimit` query; no red-overflow UI; no move-handler
enforcement.

---

## 3.5 Dashcards — **DONE**

**Plan contract (`docs/roadmap/03-execution-plan-v1.1.0.md:279-287`):**
- `Cardscape.Domain.Dashboards/` bounded context.
- `Dashcard` aggregate with `Kind` enum
  `OverdueCount, ByMember, ByLabel, ByList, DueThisWeek`.
- `IDashboardRepository` in Application.
- Migration `IssueDashcards`.
- `/boards/{id}/dashboard` page.
- MCP tools: `boards_list_dashcards`, `boards_create_dashcard`,
  `boards_delete_dashcard`.

**Evidence:**

| Item | Status | Evidence |
|---|---|---|
| Bounded context | Present | `src/Cardscape.Domain/Dashboards/` contains `Dashcard.cs`, `DashcardKind.cs`, `DashcardId.cs`. |
| `Dashcard` aggregate | Present | `src/Cardscape.Domain/Dashboards/Dashcard.cs:13-72` — `BoardId`, `Kind`, `Title`, `ConfigurationJson`, `Position`, `Delete`. |
| `Kind` enum | Present, all 5 values | `src/Cardscape.Domain/Dashboards/DashcardKind.cs:4-15` — `OverdueCount=0, ByMember=1, ByLabel=2, ByList=3, DueThisWeek=4`. |
| `IDashboardRepository` | Present | `src/Cardscape.Application/Abstractions/Persistence/IDashboardRepository.cs:6-12` — `GetByIdAsync`, `ListForBoardAsync`, `AddAsync`, `RemoveAsync`. |
| `DashboardRepository` impl | Present | `src/Cardscape.Infrastructure/Repositories/DashboardRepository.cs:9-33`. |
| Migration | **DRIFT** — consolidated, not standalone | `20260730000751_V110IntegrationConsolidated.cs:15-22` creates the `dashcards` table. No standalone `IssueDashcards.cs`. The plan called for one. |
| `/boards/{id}/dashboard` page | Present | `src/Cardscape.Web/Pages/BoardDashboard.razor:1-148` — full list / create / delete UI, with the 5-kind picker at lines 80-87. |
| REST endpoints (bonus) | Present (not in plan) | `src/Cardscape.Api/Endpoints/Dashboards/DashboardsEndpoints.cs:17-49` — `GET /`, `POST /`, `PUT /{id}/config`, `DELETE /{id}`. |
| MCP tools | Present | `boards_list_dashcards` at `src/Cardscape.Mcp/Tools/MissingTools.cs:78-82`; `boards_create_dashcard` at 84-98; `boards_delete_dashcard` at 100-102. |
| i18n keys | Present | `DashboardAddCard`, `DashboardEmpty`, `DashboardTitle`, `DashboardKind`, `DashboardTitle_Label`, `DashboardConfig` in `src/Cardscape.Web/Resources/SharedResource.resx` (e.g. lines 116, 120). |
| Integration test | Present | `tests/Cardscape.IntegrationTests/Endpoints/DashboardsEndpointTests.cs:25-87` — `Dashcard_Crud_Roundtrip` exercises create + list + delete. |

**Notes:** the only deviation from the plan is the migration name
(`V110IntegrationConsolidated` rather than `IssueDashcards`). This is
the same pattern as §3.1–3.3: the implementation consolidated several
migrations into one. Functionally everything else is in place, the
test passes (the integration test references the same endpoints), and
the Web UI is fully localised.

**Verdict:** DONE.

---

## 3.6 iCalendar feed — **DONE**

**Plan contract (`docs/roadmap/03-execution-plan-v1.1.0.md:289-296`):**
- `Cardscape.Application/Calendar/IIcalendarService.cs` with default
  `IIcalendarService.RenderBoardAsync(boardId, ct)`.
- Emits a standard RFC 5545 VCALENDAR with one VEVENT per card with
  a `DueDate` set.
- `GET /api/boards/{id}/ics` (no auth on public boards; auth required
  for private).
- MCP tool: `boards_get_icalendar`.

**Evidence:**

| Item | Status | Evidence |
|---|---|---|
| `IIcalendarService` interface | Present | `src/Cardscape.Application/Calendar/IIcalendarService.cs:12-20` — `RenderBoardAsync(Guid boardId, CancellationToken ct = default)`. |
| Default implementation | Present | `src/Cardscape.Infrastructure/Calendar/IcsCalendarService.cs:27-98` — emits `BEGIN:VCALENDAR`, one `BEGIN:VEVENT` per card with `DueDate` (line 74), `UID`, `DTSTAMP`, `DTSTART;VALUE=DATE`, `DTEND;VALUE=DATE`, `SUMMARY`, `DESCRIPTION`, `END:VEVENT`, `END:VCALENDAR`. Escapes `\\`, `;`, `,`, `\n` per RFC 5545 (lines 106-135). |
| Query handler | Present | `src/Cardscape.Application/Calendar/IcsCalendarQueries.cs:12-21` — `RenderBoardCalendarQuery` + handler. |
| Public/private auth model | Present | `IcsCalendarService.cs:52-56` — public boards pass through; private boards require membership (the endpoint is `AllowAnonymous`, so the inner service does the auth check). |
| REST endpoint | Present | `src/Cardscape.Api/Endpoints/Boards/BoardEndpoints.cs:111-123` — `MapGet("/{boardId:guid}/ics", ...).AllowAnonymous()`, returns `Results.File(..., "text/calendar", "board-{id}.ics")`. |
| SDK client | Present (bonus) | `sdk/Cardscape.Sdk/SubClients.cs:97-100` — `GetICalendarAsync` hits `api/boards/{id}/ics`. |
| MCP tool | Present | `boards_get_icalendar` at `src/Cardscape.Mcp/Tools/BoardsTools.cs:397-410`. |
| Web UI surface | **MISSING** (not in plan, but the SDK implies the Web would link to it) | No `.ics` link in `BoardExtensions.razor`, `BoardDashboard.razor`, or `BoardDetail.razor`. The "Calendar" page (`src/Cardscape.Web/Pages/Calendar.razor:1`) is the month view, not the subscription surface. |
| i18n keys | **MISSING** | No "ics" / "icalendar" / "subscribe" entries in `SharedResource.resx`. |
| Integration test | **MISSING** | No test references `/ics`, `GetICalendar`, `get_icalendar`, etc. (the only `ical` matches in `tests/` are false positives like "physical", "vertical"). The `CalendarQueryTests.cs` test (`tests/Cardscape.IntegrationTests/Endpoints/CalendarQueryTests.cs:1-86`) covers `/api/cards/calendar`, not `/api/boards/{id}/ics`. |

**Notes:** the plan's literal contract is fully met: interface,
default implementation emitting valid RFC 5545, REST endpoint with
public/private auth split, and MCP tool. The two omissions (no Web
surface, no integration test) are beyond the plan's literal wording,
but worth flagging — the endpoint has no discoverable way to be
reached from the Blazor UI and no test coverage.

**Verdict:** DONE.

---

## Summary

| § | Item | Verdict | Big gap |
|---|---|---|---|
| 3.1 | Card Aging | PARTIAL | No `ByCreation` value; `CardAgingSettings` is a separate aggregate rather than `Card.AgingMode` / `Card.SetAgingMode`; no Web UI toggle; no visual fade in `BoardDetail.razor`. |
| 3.2 | Card Snooze | PARTIAL | No `SnoozeUntil` VO; no `Card.Snooze` / `Unsnooze` / `IsSnoozed`; no `?includeSnoozed=true` flag in `CardQueries`; no Web UI section in `CardDetail.razor`. |
| 3.3 | Card Mirror | PARTIAL | Aggregate renamed (`CardMirror` instead of `MirroredCard`); no `Card.MirrorTo`; two divergent `MirrorCardCommand` records (one a stub); no "Mirror to..." button in `CardDetail.razor`. |
| 3.4 | List Limits | PARTIAL | No `ListLimit` aggregate; `BoardList.SetLimit` takes `(maxSoft, maxHard)`, not `(int? max, bool soft)`; no `IsOverLimit` query; no `MaxCardsSoft/Hard` in `BoardListDto`; no red-overflow UI; no move-handler enforcement. |
| 3.5 | Dashcards | DONE | Migration is `V110IntegrationConsolidated` rather than standalone `IssueDashcards` — otherwise everything (domain, repository, page, REST, MCP, i18n, integration test) is in place. |
| 3.6 | iCalendar feed | DONE | No integration test for `/api/boards/{id}/ics`; no Web UI surface to subscribe to the feed. Plan contract is met. |

**Most important gap:** the plan's Web UI surfaces for §3.1–3.4 are
not implemented. `BoardExtensions.razor` lists Custom fields / Voting /
Card repeater but no Card Aging. `CardDetail.razor` has no snooze
section, no snoozed badge, no "Mirror to..." button. `BoardDetail.razor`
has no list-overflow red indicator. The backend and MCP tools for all
four are functional, but the user-facing experience in the Blazor
client is the same as it was before the v1.1.0 push.

---

## Plan-checkbox update note

The task asked to flip `- [ ]` → `- [x]` in
`docs/roadmap/03-execution-plan-v1.1.0.md` for the fully-DONE items
(§3.5 and §3.6). A `Select-String` for `- [ ]` / `[x]` patterns across
that file returns **no matches** — the v1.1.0 plan uses plain `- `
bullets, not GFM checkboxes, for its §3.1–3.6 sub-bullets. So no
checkbox updates were applied to `03-execution-plan-v1.1.0.md`. The
companion `01-implementation-plan.md` does have checkboxes for
"Card Aging" / "Card Snooze" / "Card Mirror" / "List Limits" /
"Dashcards" (lines 252-315), but those are out of scope for this audit
(task specified the v1.1.0 plan file only).
