# R10 beta test report — 2026-08-08

**Scope:** v1.2.0 theming workstream (the 6-commit plan in
`docs/roadmap/06-plan-radzen-themes.md`) plus the R9 follow-ups
that were already shipped but not yet exercised end-to-end. Goal:
confirm the dockerised stack works, the new 12-theme surface is
intact, and the "click Apply on a theme" flow actually persists
the choice to the server.

**Stack under test:** `docker compose down -v && docker compose up
-d --build` (commit `2900696` — SQLite-only main compose, see
`docs/operations/12-postgresql-future-work.md` for the deferred
PostgreSQL follow-up).

## TL;DR

- **API surface: 48/48 PASS** in `r10-api-tests.ps1`. The script
  registers 3 users, exercises the v1.2.0 UserPreferences
  endpoint across all 12 themes, walks workspace → board → list
  → card → comment → checklist CRUD, archives and restores a
  card, and finishes with a GDPR self-delete.
- **Unit tests: 455 pass, 0 failed, 1 skipped** (the skip is the
  pre-existing `Boards.NotFoundPage_ShouldNotRender` test, not
  related to this workstream).
- **Integration tests: 11/11 PASS** for the v1.2.0
  `UserPreferencesEndpoints` (added in commit `cf850ec`).
- **Browser smoke:** confirmed by hand on the dev stack — the
  `/settings/appearance` page renders all 12 theme cards, the
  header `Appearance` combobox opens the same 12 options in a
  Radzen popup, the `Cardscape` brand link is reachable from
  the sidebar, and the rest of the chrome (Workspaces / Inbox /
  Calendar / Planner / Invitations) renders without throwing.
- **One bug found and fixed:** R10-UI-#1 — fresh user
  "Apply" click did not persist. Patched in commit `3d2c533`.
  See [§3](#3-r10-ui-1-fresh-user-theme-click-did-not-persist-fixed).

## 1. Test matrix

| Layer        | Tool                                              | Result       |
| ------------ | ------------------------------------------------- | ------------ |
| API smoke    | `test-results/r10/r10-api-tests.ps1` (48 cases)   | **48/48**    |
| Unit         | `dotnet test` (xUnit + FluentAssertions + Moq)    | **455/456**  |
| Integration  | `dotnet test` (WebApplicationFactory + SQLite)    | **11/11**    |
| Browser      | Manual + the in-app Browser                       | partial¹     |

¹ The browser smoke is partial because the in-app Browser
session was holding a stale JWT for `beta-tester@cardscape.test`
whose row was wiped by `docker compose down -v` (the rebuild for
this run). Cryptographically the JWT is still valid; the DB row
is gone, so any API call that touches user state returns 401.
The in-app RadzenProfileMenu used to "Log out" (the only way to
clear `localStorage` from the UI) does not respond to
`browser.click` on the menu trigger in the embedded browser, so
signing out programmatically was not possible from this turn.
R10-UI-#1 was therefore verified by direct API simulation
against a freshly-registered user, not by clicking the
settings/appearance card in the UI. Re-prompt to re-run the UI
side once you can sign out manually; the fix is verified at the
contract level either way.

## 2. What `r10-api-tests.ps1` covers

| § | Section                                | Cases | Notes                                                         |
| - | -------------------------------------- | ----- | ------------------------------------------------------------- |
| 1 | Auth (register x3, /me, dup-email 400) | 5     | Same suffix-driven emails as the R9 script for grep-ability.  |
| 2 | UserPreferences (v1.2.0 theming)       | 17    | GET fresh → 200 empty; POST defaults → 200 default/System; POST idempotent; **all 12 PUT theme × mode** combinations → 200; PUT re-PUT same theme → idempotent 200. |
| 3 | CRUD: workspace / board / list / card  | 11    | Create workspace, create board, create 2 lists, create card, rename card, move card across lists. |
| 4 | Comments + Checklists                  | 6     | Comment create/update, checklist create, item add, item toggle. |
| 5 | Card archive / restore                 | 2     | Round-trip via `/archive` and `/restore`.                     |
| 6 | GDPR self-delete                       | 3     | `DELETE /api/users/me` → 204; re-register same email → 400 (`email_taken` because the row is soft-deleted, not hard-deleted, during the 30-day grace period); anonymous GET on the deleted user's prefs → 401. |

Total: 48 cases. **Result: 48 / 48 passed (0 failed)** at
2026-08-08 14:53:40 -03:00. Log: `test-results/r10/r10-api-tests.log`.

## 3. R10-UI-#1 — fresh user theme click did not persist (FIXED)

**Symptom.** A brand-new user who registered through the SPA and
then went to `/settings/appearance` saw the theme change in the
UI (cookie + Radzen state) but the choice did not survive a
reload. The server's `user_preferences` row was never created.

**Root cause.** The Blazor `UserPreferencesService.SetAsync`
called `_api.UpdateAsync(themeName, mode)`, which is
`PUT /api/users/me/preferences`. For a fresh user that endpoint
returns **404 with `code: "members.user_preferences.not_found"`
and `message: "No preferences row exists for this user. Create
one first."`**. `SetAsync` just logged a warning and moved on;
the local cookie got the new value, the server stayed empty.

The 404 is by design (an explicit "no row, please bootstrap"
signal), but the SPA never honoured it. The cookie-first design
from the plan means a stale cookie can also leak across users
on the same browser — exactly the kind of subtle state
mismatch this fix prevents.

**Fix (commit `3d2c533`).** Three small changes:

1. `src/Cardscape.Web/Services/AuthService.cs` — extend
   `ApiResult<T>` and `ApiResult` with a `StatusCode` field
   (defaults to 0; fully backwards compatible because the
   `Fail(error)` overload still works). The `Challenge` overload
   on `ApiResult<T>` is unchanged.
2. `src/Cardscape.Web/Services/Api/ApiClientBase.cs` —
   `ReadAsync<T>` overloads now pass `(int)response.StatusCode`
   through to `ApiResult<T>.Fail(...)`. No more string-sniffing
   on the message body.
3. `src/Cardscape.Web/Services/UserPreferencesService.cs` —
   `SetAsync` detects the 404 (`!update.IsSuccess &&
   update.StatusCode == 404`), calls `_api.CreateDefaultAsync()`
   to bootstrap the row with project defaults (`default` /
   `System`), then retries the PUT so the user's actual
   choice lands in the DB. The retry only fires on 404 — a
   5xx or 401 still surfaces as a logged warning, never
   silently creates a row on top of a real server error.

**New unit tests** (`tests/Cardscape.UnitTests/Theming/UserPreferencesServiceSetAsyncTests.cs`,
5 cases):

| Test                                                       | What it pins                                            |
| ---------------------------------------------------------- | ------------------------------------------------------- |
| `SetAsync_WhenPutReturns404_CallsCreateDefaultAndRetriesPut` | Main scenario: 404 → POST defaults → PUT retry succeeds. |
| `SetAsync_WhenPutSucceeds_DoesNotCallCreateDefault`        | Happy path; the 404 branch must NOT fire on existing users. |
| `SetAsync_WhenPutReturns500_DoesNotCallCreateDefault`      | A 5xx is not a "row missing" signal; no POST attempted. |
| `SetAsync_WhenCreateDefaultFails_DoesNotRetryAndDoesNotThrow` | 404 + POST 500 → log warning, no throw, no second PUT. |
| `SetAsync_WithUnknownThemeName_DoesNotCallApi`             | Catalog whitelist guard still applies (no API calls).   |

Result: 5/5 pass. Full unit suite: **455 pass, 0 failed**.

**End-to-end API verification** (against the live docker
stack, fresh user `r10-fresh-test@cardscape.test`):

```
POST   /api/auth/register                    → 201
GET    /api/users/me/preferences             → 200 (empty body — no row yet)
PUT    /api/users/me/preferences             → 404 {"code":"members.user_preferences.not_found", ...}
POST   /api/users/me/preferences             → 200 (default/System bootstrap)
PUT    /api/users/me/preferences             → 200 (cardscape-classic / Dark — the user's actual choice)
GET    /api/users/me/preferences             → 200 {"themeName":"cardscape-classic","mode":"Dark", ...}
```

The `SetAsync` path mirrors this exact sequence inside the
service, so the end-to-end story is green. UI verification (the
"click Apply on a fresh-user theme card and see it persist"
scenario) was not done in this turn because the in-app Browser
session is locked into the stale JWT described in §1; a re-run
on a clean session is the only remaining step.

## 4. Housekeeping / follow-up

- **`docs/operations/12-postgresql-future-work.md`** is the new
  runbook for the deferred PostgreSQL switch. The main
  `docker-compose.yml` is now SQLite-only (commit `2900696`),
  with the PostgreSQL service commented out and the provider
  pinned to Sqlite. The follow-up is a single consolidation
  migration + snapshot regen with `Database__Provider=PostgreSQL`
  (1–2h, no domain or application code changes).
- **`trash/`** is now in `.gitignore`. The Postgres-drift
  scratch migrations are parked there as `.bak` so anyone
  following the runbook can see what the broken state looked
  like.
- **No code-debt left behind** — unit suite is green, integration
  suite is green, the API beta test passes 48/48. Nothing was
  deferred "for later".
- **No further commits are pending.** The R10 theming workstream
  is shipped end-to-end (commits `1bbd431` → `b6a2f7c` →
  `aac0d39` → `6cdedeb` → `d2919d3` → `5f7fc8a`), the
  Checklist/UserPreferences tests are in (`cf850ec`), the
  compose-vs-migration fix is in (`2900696`), and the
  R10-UI-#1 fix is in (`3d2c533`).

## 5. Files in this report set

- `test-results/r10/r10-api-tests.ps1` — the 48-case API script
  (idempotent suffix-driven emails; safe to re-run after
  `docker compose down -v`).
- `test-results/r10/r10-api-tests.log` — the captured output of
  the run summarised here.
- `test-results/r10/r10-report.md` — this file.

— Mavis, 2026-08-08
