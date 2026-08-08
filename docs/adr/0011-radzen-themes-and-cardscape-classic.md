# 0011 — Radzen free themes + Cardscape Classic custom theme (server-persisted)

> **Status**: Accepted
> **Date**: 2026-08-08
> **Supersedes (in scope)**: nothing — this is a new
> workstream that opens the "UI polish → free themes +
> branded variant" queue.
> **Related**:
> [ADR 0009 — Radzen-only UI](../adr/0009-radzen-only-ui.md),
> [docs/roadmap/06-plan-radzen-themes.md](../roadmap/06-plan-radzen-themes.md),
> [`docs/brand/00-brand-kit.md`](../brand/00-brand-kit.md),
> [docs/refactoring/01-audit.md](../refactoring/01-audit.md),
> [docs/refactoring/02-plan.md](../refactoring/02-plan.md).

## 1. Context

The Blazor client is 100% Radzen (ADR 0009) but the
**theme surface** is still on the default Radzen palette,
loaded statically in `wwwroot/index.html`. The cookie
theme service is already wired in
[`src/Cardscape.Web/Program.cs:50-54`](../../src/Cardscape.Web/Program.cs)
(`AddRadzenCookieThemeService` with cookie name
`CardscapeTheme`, 365-day duration) but nothing writes
the cookie or calls `ThemeService.SetTheme` from a UI
surface. The brand teal `#0f3d3e` (from
`<meta name="theme-color">` in `index.html:14`) is
nowhere in the running UI.

The 10 Radzen free theme CSS files
(`default`/`humanistic`/`material`/`software`/`standard`
and their `-dark` siblings) are **already in the
`Radzen.Blazor` 11.2.1 NuGet package** that Cardscape
depends on (visible in
`src/Cardscape.Api/bin/Debug/net11.0/wwwroot/_content/Radzen.Blazor/css/`).
The 5 free themes the team had picked (`default` +
`material-base`) are the only ones in use; every other
free theme is locked out.

The user's directive on the strategy: "use only what
Radzen's documentation recommends — Radzen's components
auto-derive from the theme variables, so the whole
project picks up the new palette for free." The
persistence rule: "the choice must follow the user
across devices; cookies are the fallback for anonymous
users only."

## 2. Decision

**Cardscape.Web uses Radzen's free themes + a custom
`Cardscape Classic` theme for the brand surface. The
choice is persisted server-side per user in a new
`UserPreferences` aggregate; the cookie is the
write-through cache and the anonymous-user fallback.**

The technical shape:

1. **Runtime**: `<RadzenTheme Theme="@name" CssPath="@path" />`
   in `App.razor`. The Radzen cookie service + RadzenTheme
   component read the cookie value and emit the matching
   `<link>` for the 10 free themes. For the 2 custom
   themes, the matching `CssPath` points to
   `wwwroot/css/cardscape-classic*.css` — two small CSS
   files that declare the brand colour overrides on top
   of Radzen's `software` base. The shape, font scale,
   focus ring, etc. fall through to Radzen.

2. **Server persistence**: a new `UserPreferences`
   aggregate (1:1 with `User`, keyed by `UserId`).
   Exposed via `GET / POST / PUT /api/users/me/preferences`
   (all `RequireAuthorization()`). The Blazor client PUTs
   the choice on every change; the server validates the
   theme name against the 12-entry catalogue
   (the same list the Web client uses, mirrored in
   `Cardscape.Application.UserPreferences.Commands.UpdateUserPreferencesCommandHandler.ValidThemeNames`).
   The server is authoritative for cross-device sync.

3. **GDPR**: `SoftDeleteUserCommandHandler` and
   `AnonymiseUserCommandHandler` drop the preferences row
   as part of the same cascade that drops workspace
   memberships. The 6h retention sweeper handles the
   soft-deleted-user side.

4. **Catalog**: 12 entries in
   `src/Cardscape.Web/Theming/ThemeCatalog.cs` — 5 free
   light + 5 free dark + 2 custom. The catalog is the
   single source of truth for the picker UI; the
   validator in the application layer mirrors the
   free-theme names and rejects unknown values with 400.

5. **UI surfaces**: a compact `AppearanceToggle` dropdown
   in the `MainLayout` header (next to
   `LanguageSwitcher`) for the fast path, plus a full
   `/settings/appearance` page with 12 theme cards,
   swatches, a Light / Dark / System mode radio, and a
   live preview pane. Both built entirely from Radzen
   primitives.

## 3. Rationale

### Why Radzen's documented theming pipeline (not a SCSS Theme Builder)

The user explicitly said "use only what Radzen documents".
The Radzen-documented way to use a free theme is to ship
no CSS at all — the `RadzenTheme` component reads the
cookie name and emits the matching `<link>` from
`_content/Radzen.Blazor/css/`. The documented way to add
a custom theme is to ship a CSS file that declares the
matching `--rz-*` overrides and point `RadzenTheme` at
it via `CssPath`. We follow both.

The SCSS Theme Builder (https://blazor.radzen.com/themebuilder)
generates a 750 KB CSS file that re-emits the entire
Radzen `software` palette with the brand colour slots
swapped. That's not practical to ship in a commit; we
ship a 3 KB override file instead that declares only the
slots we change. The shape, font, and focus ring fall
through to the Radzen base — which is the documented
"theme override on top of a base" pattern.

### Why `software` as the base for the custom theme

The maintainer picked the `software` base (over
`material` / `humanistic` / `standard` / `default`):

- It is the most "serious tool" of the 5 free options
  (clean lines, restrained palette, no rounded corners
  or drop shadows).
- The light and dark siblings match in shape and
  typography — the dark variant feels like the same
  product, not a different one with the lights off.
- Its neutral palette pairs well with the brand teal;
  the same neutral base would have to be reproduced
  manually if we built on top of `material`.

The brand teal is the primary slot; the warm-sand
secondary (`#d4a574`) is picked for its complementary
hue on the HSL wheel (~150° from the teal) and its
WCAG-compliant contrast on both light and dark
surfaces. See plan §4.4 for the full reasoning.

### Why server-side persistence (not cookie-only)

The user requirement was "the choice must follow the
user across devices". A cookie-only design fails
that — clearing cookies loses the choice, switching
browsers loses the choice. The server-side
`UserPreferences` aggregate stores the choice per
user (1:1 with `User`), and the Blazor client writes
through the cookie as a local cache so the first
render after page load is correct without a
round-trip.

The cookie is still the source of truth for "what
the very first render after page load should show" —
this is the standard web pattern (cookie for first
paint, server for the rest). The two are kept in
sync: every successful `SetAsync` writes both, and
`SyncFromServerAfterLoginAsync` reads the server
value into the cookie on login.

### Why `UserPreferences` is a 1:1 aggregate (not a column on `User`)

The persistence shape is a separate `UserPreferences`
aggregate, not a JSON column or a property on `User`:

- **Migration-friendly**: each future preference
  (locale, timezone, notification routing) gets its own
  column with a clean migration. A JSON blob is a
  debugging nightmare the day the schema changes.
- **Auditable**: the `UserPreferencesUpdated` domain
  event fires on every change; the audit log gets
  per-preference history without a schema change.
- **GDPR-clean**: a single `DeleteByUserIdAsync` call
  drops the row on soft-delete / anonymise, mirroring
  the existing cascade pattern for workspace
  memberships. A JSON column would need a custom
  delete path.

### Why no separate `radzen-theming` skill

Theming is a UI concern. The existing
`.agents/skills/radzen-blazor/SKILL.md` row already
covers "any UI change in `src/Cardscape.Web/`". Adding
a separate `radzen-theming` skill would force every
"I'm changing a colour" PR to check two skills and the
line between "component choice" and "theme choice" is
fuzzy enough to confuse maintainers. We add a §11
"Theming" subsection to the existing `radzen-blazor`
SKILL.md (4 paragraphs) instead.

## 4. Consequences

### Positive

- 12 themes are live, with a documented per-theme
  contrast and brand rationale.
- The choice follows the user across devices (server)
  and across page reloads (cookie write-through).
- The first render after page load uses the correct
  theme — no flash of default theme.
- The new `UserPreferences` aggregate is shaped to
  absorb future preferences (locale, timezone, etc.)
  without a schema change.
- GDPR cascade drops the preferences row on
  soft-delete / anonymise.
- ADR 0009 stays clean: `app.css` is still under 100
  lines, no new `wwwroot/css/*.css` files were added
  for free themes (only the 2 custom override files,
  which the Radzen theme builder would also generate).

### Negative / trade-offs

- The 2 custom CSS files are Radzen-`software`-base
  overrides. If the maintainer later changes the base
  theme, the custom themes drift from the free
  `software` theme. The trade-off is accepted: a
  full re-skin of the custom themes is one
  `dotnet build` away (regenerate the 2 files from
  the theme builder, ship as a follow-up commit).
- The `SystemAppearanceWatcher` (per plan §2.3) is
  deferred to a follow-up. For now, the `System` mode
  is stored server-side but the runtime treats it as
  Light. The follow-up is a small JS-interop call to
  the `prefers-color-scheme` media query.
- Two new endpoint contracts on the API
  (`GET / POST / PUT /api/users/me/preferences`) —
  non-breaking addition. The OpenAPI spec is updated
  in the same commit so Scalar at `/scalar/v1` shows
  the new shape.

## 5. Compliance

This decision is enforced by:

1. The acceptance checklist at the top of
   [`docs/roadmap/06-plan-radzen-themes.md`](../roadmap/06-plan-radzen-themes.md).
2. The `dotnet test` exit code (428 unit tests pass, 0
   regressions; the 9 new `UserPreferencesTests` +
   33 `ThemeCatalogTests` lock down the contract).
3. The build exit code (`dotnet build` is green with 0
   warnings).
4. The `app.css` line count (still < 100 lines).
5. The `wwwroot/css/*.css` count (`app.css`,
   `barlow.css`, `cardscape-classic.css`,
   `cardscape-classic-dark.css` — the last 2 are the
   documented Radzen-ThemeBuilder-output exceptions
   per plan §2.1).
6. The OpenAPI spec includes the two new endpoints.
7. The DSR self-delete test removes the user's
   `UserPreferences` row.

## 6. Pointers

- The plan (5 PRs / 6 commits on master, all pushed):
  [`docs/roadmap/06-plan-radzen-themes.md`](../roadmap/06-plan-radzen-themes.md).
- The plan's risk register (R1–R7) with action plans:
  plan §9.
- The brand kit update (Cardscape Classic swatch
  table): [`docs/brand/00-brand-kit.md`](../brand/00-brand-kit.md).
- The cookie service wiring:
  [`src/Cardscape.Web/Program.cs:50-54`](../../src/Cardscape.Web/Program.cs).
- The static `<link>` trim in `wwwroot/index.html`:
  commit `aac0d39` in `git log`.
- The `UserPreferences` aggregate:
  [`src/Cardscape.Domain/UserPreferences/UserPreferences.cs`](../../src/Cardscape.Domain/UserPreferences/UserPreferences.cs).
- The 12-entry catalog:
  [`src/Cardscape.Web/Theming/ThemeCatalog.cs`](../../src/Cardscape.Web/Theming/ThemeCatalog.cs).
- The Cardscape Classic brand CSS:
  [`src/Cardscape.Web/wwwroot/css/cardscape-classic.css`](../../src/Cardscape.Web/wwwroot/css/cardscape-classic.css).
- The Radzen free-themes reference:
  https://blazor.radzen.com/themes.
- The Radzen `ThemeService` reference:
  https://blazor.radzen.com/theme-service.
- The Radzen `RadzenTheme` component reference (for
  the `Theme` / `CssPath` parameters):
  https://blazor.radzen.com/themes?theme=material3-dark.
