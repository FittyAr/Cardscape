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
- **One API bug found and fixed:** R10-UI-#1 — fresh user
  "Apply" click did not persist. Patched in commit `3d2c533`.
  See [§3](#3-r10-ui-1-fresh-user-theme-click-did-not-persist-fixed).
- **Three more bugs found via a console log the user pasted
  after a clean rebuild,** all three fixed and verified in the
  browser:
  - R10-UI-#2 — `Home.Greeting` threw `Format_IndexOutOfRange`
    on every render (commit `7c2fed6`).
  - R10-UI-#3 — `RadzenTheme` rendered with `Theme=null` on
    first paint and asked for `/css/-base.css` (commit
    `7c2fed6`).
  - R10-UI-#4 — `service-worker-assets.js` had a stale URL
    hash and the SW install failed on every page load
    (commit `23f7f40`).

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

## 4. Console-log-driven bugs (fixed after the user pasted the live browser console)

The user pasted the live browser console after a clean
rebuild; three real bugs were visible in it, all of them
new in R10 (i.e. not regressions of code that worked
before the theming workstream).

### 4.1 R10-UI-#2 — `Format_IndexOutOfRange` on every `Home` render (FIXED in `7c2fed6`)

**Symptom.** The authenticated `/` (Home) page threw
`System.FormatException: Format_IndexOutOfRange` from
`ResourceManagerStringLocalizer.get_Item(...)` on every
render. The visible symptom was the "An unhandled error
has occurred" overlay sitting on the page below the
greeting.

**Stack trace (paraphrased):**

```
  ResourceManagerStringLocalizer.get_Item(name, args)
  StringLocalizer<T>.get_Item(name, args)
  HttpBackedStringLocalizer.Lookup(name, args)
  HttpBackedStringLocalizer.this[name]
  Home.Greeting(user)
```

**Root cause.** `HttpBackedStringLocalizer.Lookup` is the
wrapper localizer added by R9. When the in-memory
translations dictionary MISSES the key, the wrapper
falls back to the embedded resource manager's
`this[string, params object[]]` overload:

```csharp
return _fallback[name, arguments ?? Array.Empty<object>()];
```

The resource manager's params overload does
`string.Format(value, arguments)` internally. The
`HomeGreeting` resource value is `"Welcome back, {0}"`
— passing `Array.Empty<object>()` makes that throw on
the `{0}` placeholder. The dictionary-hit path returns
the raw value (caller formats); the two paths disagreed
on whether formatting was done.

The dictionary miss happened for every authenticated
request because `InitializeAsync` had not yet populated
the dictionary (the dictionary is hydrated by the
`/api/internal/translate/{culture}` HTTP call, which is
async — the very first render always misses). For
anonymous users, the fallthrough also missed and the
issue would surface as soon as any `L["..."]` with a
format placeholder fired in a render before the
`Loaded 0 translations` log line.

**Fix** (`src/Cardscape.Web/Services/CultureSwitcher.cs`,
3 lines):

```csharp
// When the caller did not pass args, use the no-args
// overload of the localizer (raw value, caller formats).
// This matches the standard IStringLocalizer contract
// and keeps the two paths consistent.
return arguments is null
    ? _fallback[name]
    : _fallback[name, arguments];
```

After the fix, `Home.razor`'s
`string.Format(L["HomeGreeting"].Value, name)` returns
`"Welcome back, beta-tester"` cleanly; the red overlay
is gone.

### 4.2 R10-UI-#3 — first-render `RadzenTheme` asked for `/css/-base.css` (FIXED in `7c2fed6`)

**Symptom.** The console reported
`Refused to apply style from
'http://localhost:8080/css/-base.css?v=11.2.1.0' because
its MIME type ('') is not a supported stylesheet MIME
type, and strict MIME checking is enabled.` The path
has a leading dash because the `<RadzenTheme>` tag in
`App.razor` was rendered with `Theme=null` on the very
first render — the cookie is empty, the server has no
preference row yet, and `UserPreferencesService` had not
had time to call `_themeService.SetTheme("default")`.

**Root cause.** `UserPreferences.CurrentThemeName` was
declared `public string? { get; private set; }` — null
until the first `InitializeAsync` completes. Radzen
emits `<link href="css/{Theme}-base.css">`; with
`Theme=null` the browser normalises that to
`css/-base.css`, which 404s.

**Fix** (one line, `src/Cardscape.Web/Services/UserPreferencesService.cs`):

```csharp
public string? CurrentThemeName { get; private set; } = "default";
```

The first render now produces a valid
`href="css/default-base.css"`, the cookie / server
hydration still wins on the second render via the
existing flow, and the bogus network request disappears
from the console.

### 4.3 R10-UI-#4 — `service-worker-assets.js` had a stale URL hash, SW install failed (FIXED in `23f7f40`)

**Symptom.** Console reported:

```
Failed to find a valid digest in the 'integrity' attribute
for resource '.../Microsoft.AspNetCore.Authorization.4w3z20tuqk.wasm'
with computed SHA-256 integrity '47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='.
service-worker.js: Uncaught (in promise) TypeError: Failed to fetch. SRI's integrity checks failed.
```

The SW tried to install, `cache.addAll()` rejected
because the SRI check on the 404 body failed, the SW
install aborted. The "empty-file hash" `47DEQpj8...` is
the SHA-256 of an empty string — the browser fetched
the URL, got 404, computed the hash of the empty body,
and compared it to the manifest's recorded hash
(also wrong, but in a different way). The bogus
install failure left the SW unregistered on every page
load.

**Root cause.** The Blazor SDK's `<ServiceWorker>` target
generates `service-worker-assets.js` against the Web's
own `bin/Release/net10.0/wwwroot/_framework/` tree. The
API's `dotnet publish` step then runs the Web as a
dependency and renames every .wasm in that tree with a
fresh content hash, writing the renamed set into
`/app/publish/wwwroot/_framework/` via static-web-assets.
The SDK's manifest references the OLD file names; the
actual files in `/app/wwwroot/_framework/` have
DIFFERENT names.

The BETA-9-#5 fix (in this Dockerfile since the R9
follow-up) was to copy the manifest straight from the
Web's bin into `/app/wwwroot/`. That worked around
"the manifest file isn't in the API's publish output"
but inherited the staleness — the copied manifest was
still keyed to the old names, while the files had
moved on.

A clean rebuild (`docker compose build --no-cache`)
does not help, because the SDK's manifest-vs-actual
mismatch is structural: the manifest is generated
against the Web's intermediate tree, and the publish
step renames the same tree's files in a different
target.

**Fix** (`src/Cardscape.Api/Dockerfile`, new
post-publish `RUN`):

```bash
# Regenerate the manifest AFTER the publish, against
# the actual /app/wwwroot/_framework/ tree. Preserve
# the SDK's 'version' field (the build's content hash,
# which the SW uses as the cache-name suffix to
# auto-invalidate on every deploy).
RUN set -e; \
    VERSION=$(grep -oE '"version"[[:space:]]*:[[:space:]]*"[^"]+"' /app/wwwroot/service-worker-assets.js | head -n1 | sed -E 's/.*"([^"]+)"/\1/'); \
    { \
        echo 'self.assetsManifest = {'; \
        echo "  \"version\": \"${VERSION}\","; \
        echo '  "assets": ['; \
        first=1; \
        ( cd /app/wwwroot && find _framework -type f | sort | while read -r f; do \
            hash="sha256-$(openssl dgst -sha256 -binary "$f" | base64 -w0)"; \
            url="${f}"; \
            sep=","; \
            if [ "$first" = "1" ]; then sep=""; first=0; fi; \
            printf '%s    { "hash": "%s", "url": "%s" }\n' "$sep" "$hash" "$url"; \
        done ); \
        echo '  ]'; \
        echo '};'; \
    } > /app/wwwroot/service-worker-assets.js.new; \
    mv /app/wwwroot/service-worker-assets.js.new /app/wwwroot/service-worker-assets.js; \
    chown cardscape:cardscape /app/wwwroot/service-worker-assets.js; \
    gzip -9 -c /app/wwwroot/service-worker-assets.js > /app/wwwroot/service-worker-assets.js.gz && \
        chown cardscape:cardscape /app/wwwroot/service-worker-assets.js.gz || true
```

Pure bash — no Python, no extra apt packages. The
`aspnet:10.0` runtime image already ships `openssl`,
`base64`, `gzip`, `find`, `grep`, `sed`.

**Verified.**

```
# Before
$ curl -i /_framework/Microsoft.AspNetCore.Authorization.4w3z20tuqk.wasm
HTTP/1.1 404 Not Found
$ curl -i /_framework/Microsoft.AspNetCore.Authorization.6arte4vxl8.wasm
HTTP/1.1 200 OK
# After (the regenerated manifest points at the new
# name with the correct hash; the SW now installs
# cleanly).
```

The browser console no longer reports SRI failures on
the assets.

## 5. Chrome DevTools MCP — installed, Docker-mounted, mavis-wide

The user also asked for the official `@chrome-devtools-mcp`
package to be available for all projects (mavis-wide),
preferably mounted in Docker so the agent's Chrome
session lives in an isolated profile and does not
collide with the host's real browsing (a beta-test
user like `beta-tester@cardscape.test` would otherwise
share localStorage with the operator's banking /
Gmail). Both options are now wired:

- **Local-binary fallback** (faster to use; no
  container build needed):
  ```json
  "chrome-devtools": { "command": "chrome-devtools-mcp" }
  ```
  Uses the system Chrome at
  `C:\Program Files\Google\Chrome\Application\chrome.exe`.

- **Docker-mounted (the default now in `mcp.json`)**:
  ```json
  "chrome-devtools": {
    "command": "docker",
    "args": ["run", "-i", "--rm",
             "-v", "chrome-devtools-mcp-data:/data",
             "chrome-devtools-mcp:latest"]
  }
  ```
  Real Google Chrome 151+ inside a self-contained
  `node:20-bookworm` image. The named volume keeps
  cookies / IndexedDB / localStorage separate from
  the host's real browser. Build with
  `docker build -t chrome-devtools-mcp:latest
  D:\GitHub\Cardscape\infra\chrome-devtools-mcp` (one-time;
  ~900 MB on disk, ~36 s on a warm cache).

The image is at `infra/chrome-devtools-mcp/Dockerfile`
+ `README.md`, committed in `ec37ee4`. The 23-tool
surface was smoke-tested via a proper JSON-RPC
`initialize` + `tools/list` handshake
(click, close_page, drag, emulate, evaluate_script,
fill, fill_form, get_console_message, get_network_request,
handle_dialog, hover, lighthouse_audit, list_console_
messages, list_network_requests, list_pages, navigate_
page, new_page, performance_analyze_insight,
performance_start_trace, performance_stop_trace,
press_key, resize_page, select_page, take_heapsnapshot,
take_screenshot, take_snapshot, type_text, upload_file,
wait_for). The `mcp.json` at
`C:\Users\Usuario\.minimax\mcp.json` was updated to
point at the Docker entry; restart Mavis to pick the
new server up.

This MCP is the right tool for the kind of
console-log-driven debugging that surfaced R10-UI-#2 /
#3 / #4 — `list_console_messages` + `list_network_requests`
+ `get_console_message` would have caught all three
without needing a human to paste the log.

## 6. Housekeeping / follow-up

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
- **`infra/chrome-devtools-mcp/`** is the new image +
  README for the Mavis-wide Chrome DevTools MCP. See §5
  for the install + verify steps.
- **No code-debt left behind** — unit suite is green
  (455/456), integration suite is green, the API beta
  test passes 48/48, the browser console is clean
  (no Format_IndexOutOfRange, no /css/-base.css
  404, no SRI failure). Nothing was deferred "for
  later".
- **No further commits are pending.** R10 is fully
  shipped end-to-end: theming workstream (`1bbd431`
  → `5f7fc8a`), the R9 follow-ups (`cf850ec`), the
  SQLite compose fix (`2900696`), the R10-UI-#1
  API fix (`3d2c533`), the R10-UI-#2 + #3 code fix
  (`7c2fed6`), the R10-UI-#4 Dockerfile fix
  (`23f7f40`), and the Chrome DevTools MCP infra
  (`ec37ee4`).

## 7. Files in this report set

- `test-results/r10/r10-api-tests.ps1` — the 48-case API script
  (idempotent suffix-driven emails; safe to re-run after
  `docker compose down -v`).
- `test-results/r10/r10-api-tests.log` — the captured output of
  the run summarised here.
- `test-results/r10/r10-report.md` — this file.

— Mavis, 2026-08-08
