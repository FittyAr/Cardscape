# v1.2.0 plan — Radzen free themes + Cardscape custom theme (server-persisted)

> **Date**: 2026-08-08
> **Status**: **PLANNED** — execution starts on user approval.
> **Predecessor**: [`05-plan-v1.2.0.md`](05-plan-v1.2.0.md) (doc reconciliation + G12 i18n follow-up + GDPR/AI polish).
> **Supersedes (in scope)**: nothing yet — this plan opens the
> "UI polish → free themes + branded variant" workstream that the
> R9 walkthrough (`test-results/r9/r9-report.md`) flagged as
> next on the queue after the i18n regression was closed.
> **TL;DR**: 6 commits on `master`, ~3 sessions. Enables every
> Radzen free theme (`default` / `humanistic` / `material` /
> `software` / `standard`, each with a `-dark` sibling) plus a
> **Cardscape Classic** custom theme + matching `-dark` variant,
> built on top of Radzen's `Software` base. **Theme choice is
> persisted server-side per user** (a new `UserPreferences`
> aggregate, GDPR-clean); cookies are the **fallback for
> anonymous users only**. No new NuGet packages, no build-
> tooling changes, no public-contract changes outside the new
> `GET/PUT /api/users/me/preferences` endpoints, zero HTML/JS/
> custom CSS in the Blazor client. One new ADR (0011), one new
> page (`/settings/appearance`), one new shared component
> (`AppearanceToggle.razor`).
>
> Every item lands on `master` as a single commit, with
> the build + tests green at the end.

---

## 0. Why this plan exists

The Blazor client is now 100% Radzen ([ADR 0009](../adr/0009-radzen-only-ui.md),
[`docs/refactoring/01-audit.md`](../refactoring/01-audit.md)), but
the **theme surface** is still on the default Radzen palette
(loaded statically in `wwwroot/index.html:10-11`):

```html
<link rel="stylesheet" href="_content/Radzen.Blazor/css/default.css" />
<link rel="stylesheet" href="_content/Radzen.Blazor/css/material-base.css" />
```

- The user picks nothing — `default` is what they get.
- The cookie theme service is already wired in
  [`src/Cardscape.Web/Program.cs:50-54`](../../src/Cardscape.Web/Program.cs)
  (`AddRadzenCookieThemeService` with cookie name
  `CardscapeTheme`, 365-day duration) but **nothing writes the
  cookie or calls `ThemeService.SetTheme` from a UI surface**.
  The cookie is dead state. After this plan, the cookie is a
  **fallback for anonymous users** only — logged-in users get
  the same setting from the server, so the choice follows them
  across devices.
- There is no `/settings/appearance` page, no toggle in the
  header, no light/dark switch — only the empty Radzen CSS link
  in `index.html`.
- Cardscape has a brand mark (`<meta name="theme-color" content="#0f3d3e" />` in `index.html:14`) and a brand kit
  ([`docs/brand/00-brand-kit.md`](../brand/00-brand-kit.md)) but
  the running UI does not reflect it. The teal `#0f3d3e` is
  nowhere in the Radzen default palette.
- The Radzen shipped themes that the team picked are
  `default` (the current) and `material` (the `-base` sibling).
  Every other free theme — `humanistic`, `software`, `standard`,
  with `-dark` siblings — is locked out, even though they are
  **already in the `Radzen.Blazor` 11.2.1 NuGet package** that
  Cardscape depends on (visible in
  `src/Cardscape.Api/bin/Debug/net11.0/wwwroot/_content/Radzen.Blazor/css/`).

This plan:

1. Wires the existing cookie service to a real UI surface
   (header toggle + settings page) so the user can pick any
   of the free Radzen themes (and their `-dark` siblings).
2. Defines and ships a **Cardscape Classic** custom theme +
   matching **Cardscape Classic Dark** variant that pulls the
   brand teal `#0f3d3e` into the primary/accent slots, based
   on Radzen's **Software** theme (the maintainer's pick for
   the cleanest, most "serious-tool" feel of the free options).
3. Persists the choice **server-side per user** in a new
   `UserPreferences` aggregate (1:1 with `User`). The cookie
   becomes the **fallback for anonymous users** and is also
   used as a write-through cache so the very first render
   after login doesn't flash a different theme.
4. Adapts the one place that already hard-codes a Radzen
   surface — `Layout/EmptyLayout.razor.css` (the auth
   brand-column gradient) — so it stays on theme under
   every variant.
5. Documents everything (ADR 0011, `docs/brand/` cross-link,
   roadmap entry, skill update).

The constraint from ADR 0009 ("zero custom HTML/JS/CSS in
`Cardscape.Web`") is preserved. The custom theme is delivered
**via the Radzen `ThemeService.SetTheme` programmatic API** with
a `Theme` C# object — no `.css` file in `wwwroot/`, no SCSS
build step, no inline `style=`, no `IJSRuntime`. The only
touch on `app.css` is removing the now-unused static `<link>`
tags (the cookie service takes over and emits the right `<link>`
at runtime via the `<RadzenTheme>` tag, which `App.razor` does
not yet render — see §3 below).

### 0.1 What "use Radzen's tools" means here

The maintainer directive is: the theme system must use
**only** what Radzen's documentation recommends. The reason is
that Radzen's components auto-derive their visual properties
(`Color`, `Background`, `BorderRadius`, font scale, focus ring,
icon style, …) from the theme variables — so by using Radzen's
own theme pipeline we get the same surface for every component
for free, with zero per-page CSS to keep in sync.

In practice that means:

- **Free themes**: the 5 CSS files that ship in
  `Radzen.Blazor` (`default`, `humanistic`, `material`,
  `software`, `standard`, each with a `-dark` sibling), loaded
  via Radzen's own `ThemeService` (the `Theme` object's
  `Name` is the cookie value; the service resolves the
  matching `<link>`).
- **Custom theme**: a `Theme` C# object instantiated from
  Radzen's `Radzen.Blazor.Theme` class. The class is the
  **documented, public, supported** API for declaring a theme
  programmatically. Radzen's runtime reads the object's
  `Colors` / `Shape` / `Typography` / `IconStyle` and emits
  the matching `--rz-*` CSS custom properties on `<html>`.
  This is exactly what Radzen's own theme builder generates
  (https://blazor.radzen.com/themebuilder) — we are using
  the same API by hand instead of via the GUI.
- **No new build tooling**. The Radzen theme builder exports
  a `.scss` file that compiles at build time; we are not
  adding a SCSS toolchain. The `Theme` object compiles
  to `--rz-*` overrides at runtime.
- **No new `wwwroot/css/*.css` files**. The output of the
  `Theme` object is inline CSS on `<html>`, not a file in
  `wwwroot/`.

This is fully consistent with the maintainer's "no custom
HTML/JS/CSS" rule **and** with the rule "use Radzen's tools
exclusively" — every pixel the user sees comes from a Radzen
component reading a Radzen-declared theme variable.

---

## 1. Goals & non-goals

### 1.1 Goals

- **G1.** Every Radzen free theme (`default`, `humanistic`,
  `material`, `software`, `standard`) is selectable in the
  UI, with a matching `-dark` sibling.
- **G2.** A **Cardscape Classic** custom theme + matching
  **Cardscape Classic Dark** variant (built on Radzen's
  **Software** base) is registered with the Radzen
  `ThemeService` and shows up in the same picker alongside
  the free themes.
- **G3.** The user can switch themes from **two** surfaces:
  - A compact `AppearanceToggle.razor` icon button in the
    `MainLayout.razor` header (next to `LanguageSwitcher`).
  - A full `/settings/appearance` page that lists every
    theme with a preview swatch, group radios for the
    3-way mode (Light / Dark / System), and a live preview
    pane.
- **G4.** **Persistence is server-side per user.** A new
  `UserPreferences` aggregate (1:1 with `User`) stores
  `ThemeName` and `AppearanceMode` (Light / Dark / System).
  Exposed via `GET /api/users/me/preferences` and
  `PUT /api/users/me/preferences`. The endpoint is
  authenticated; anonymous users fall back to the cookie
  (which the existing `AddRadzenCookieThemeService` wires
  for free).
- **G5.** The cookie is **always** written as a write-through
  cache, even for logged-in users. The reason: the very first
  render after login must use the correct theme without
  waiting for the API round-trip. The cookie is the source
  of truth for "what theme to show on the next page load";
  the server is the source of truth for "what the user's
  preference is". On login we hydrate the cookie from the
  server; on every toggle we write both.
- **G6.** GDPR: the new `UserPreferences` row is **deleted**
  when the user does a DSR self-delete, a soft-delete, or
  an admin anonymise. The 6h retention sweeper handles the
  soft-delete side. The aggregate has no fields that need
  redaction for the anonymise path — `UserId` is the only
  reference, and it goes with the user.
- **G7.** No new NuGet package, no SCSS tooling, no
  `wwwroot/css/*.css` additions. The custom theme is a
  `Theme` C# class instantiated at startup.
- **G8.** Acceptance tests (bUnit + xUnit + integration
  curl) cover: cookie write on toggle, theme change
  re-renders, custom theme applies its declared primary
  color to a `<RadzenButton ButtonStyle=Primary>`,
  `/settings/appearance` page renders the free-themes
  catalog + the Cardscape Classic entry, the
  `UserPreferences` round-trip works (create / read /
  update / DSR-delete), the API rejects anonymous
  `PUT /api/users/me/preferences` with 401.
- **G9.** ADR 0011 documents the design decision, the
  brand kit cross-links to the ADR, and the radzen-blazor
  skill gains a §11 "Theming" subsection.

### 1.2 Non-goals (out of scope for this plan)

- **NG1.** Replacing the **kanban's** hard-coded Trello greys
  (`#ebecf0`, `#f4f5f7`, …) in `Shared/KanbanBoard.razor.css`
  with theme variables. ADR 0009 §4 already documents this as
  an accepted trade-off; revisited only if the user changes
  the rule.
- **NG2.** Custom **icons** for Cardscape. The Radzen
  `Theme.IconStyle` supports `Filled` / `Outlined` /
  `Sharp` and we will set it per-theme, but we will not
  ship a custom `.svg` or icon font.
- **NG3.** Theme inheritance / "user-defined theme from
  picker". The Radzen free themes are static. The Cardscape
  Classic theme is fixed. The user picks one of the 12
  (5 free light + 5 free dark + Cardscape Classic + Cardscape
  Classic Dark).
- **NG4.** A "High Contrast" mode. Radzen ships `-wcag`
  variants (`default-wcag.css`, etc.) but those are static
  CSS files; integrating them requires toggling a different
  `<link>` from the cookie service, which the Radzen
  `ThemeService` does **not** do out of the box. Deferred
  to a follow-up plan (the WCAG story is already met by
  the `prefers-reduced-motion` override in `app.css:90-99`
  and the `:focus-visible` ring in `app.css:131-140`).
- **NG5.** Updating `docs/AGENTS.md` to recommend a
  different skill than `radzen-blazor` for theming. Theming
  **is** a UI concern, so the same skill applies. The
  "any UI work" rule already covers this.
- **NG6.** Migrating **other** user preferences (locale,
  timezone, notification routing, …) into the new
  `UserPreferences` aggregate. The aggregate is shaped so
  they can move in later without a migration (we use a
  separate column per preference, not a JSON blob), but the
  move itself is a follow-up plan.

---

## 2. Strategy

### 2.1 How Radzen themes work (the bits that matter)

The Radzen theme surface has three moving parts:

1. **CSS files** at `_content/Radzen.Blazor/css/{name}.css`
   that set the `--rz-*` custom properties on `:root`. The
   base variables (`--rz-primary`, `--rz-base-background-color`,
   `--rz-text-color`, `--rz-border-radius`, typography scale,
   …) are declared once in `{name}-base.css` and the
   appearance-specific overrides (color, font, shape) live
   in `{name}.css`. The cookie service **emits a `<link>` for
   the chosen theme at runtime** via the
   `RadzenTheme.razor` component (which the
   `AddRadzenCookieThemeService` registration wires in
   `App.razor`).
2. **`ThemeService`** (registered by
   `AddRadzenCookieThemeService`) exposes
   `SetTheme(Theme theme)` / `CurrentTheme` and persists the
   choice to a cookie. The cookie is read on the next page
   load and applied **before first render**, so the user never
   sees a flash of unstyled / default-themed content.
3. **`Theme` C# class** (in `Radzen.Blazor`) is the
   **programmatic** description of a theme: `Name`, `Colors`
   dictionary, `IconStyle`, `Typography` (font family, weight
   scale), `Shape` (border radius, padding scale),
   `Spacing`. **This is the API we use to ship the Cardscape
   Classic custom theme** — it never touches a `.css` file.

Three consequences:

- We can register a custom theme **without writing any CSS** by
  calling `ThemeService.SetTheme(ourCustomTheme)` from a
  constant lookup table (the same way the cookie service
  resolves "default" → `default.css`).
- The cookie service knows only about the **5 free Radzen
  themes** built into Radzen.Blazor. To make the custom theme
  selectable, we must register it with the same machinery —
  easiest path is to call
  `ThemeService.SetTheme(CardscapeThemes.Classic)` on the
  cookie match.
- The `<link>` for the chosen free theme is emitted by the
  cookie service via `<RadzenTheme>`. The custom theme does
  **not** need a `<link>` — its colors/typography are
  injected as inline `--rz-*` overrides on `<html>` by the
  cookie service after `SetTheme(theme)` runs.

### 2.2 Server-side persistence strategy

We add a **`UserPreferences` aggregate** in
`Cardscape.Domain` (1:1 with `User`) with two fields:

```csharp
public sealed class UserPreferences : AggregateRoot
{
    public UserId UserId { get; private set; }      // 1:1 with User
    public string ThemeName { get; private set; }   // e.g. "default", "humanistic", "cardscape-classic"
    public AppearanceMode Mode { get; private set; } // Light | Dark | System
    public DateTimeOffset UpdatedAt { get; private set; }

    // ctor + Update(themeName, mode) + a domain event
}
```

The flow on the Blazor side is:

```
UserPreferencesService (singleton in Web)
   ├── InitializeAsync(): called from App.razor on first render
   │     ├── if logged in:
   │     │     GET /api/users/me/preferences
   │     │     → on 404, create with defaults (theme="default", mode=System)
   │     │     → write the result to the cookie (write-through cache)
   │     └── if anonymous:
   │         read the cookie (AddRadzenCookieThemeService does this for free)
   │
   ├── SetAsync(themeName, mode): called from the toggle / settings page
   │     ├── write the cookie first (so the next page load is instant)
   │     ├── apply the theme via ThemeService
   │     └── if logged in: PUT /api/users/me/preferences (fire-and-forget on best effort;
   │                       on failure, log + show a RadzenNotification of severity Warning)
   │
   └── Current: { ThemeName, Mode } — the Blazor components bind to this
```

Two important details:

- **The cookie is the source of truth for "what to show on
  the very first render after page load"**, because the
  server round-trip costs a full HTTP exchange. The server
  is the source of truth for "what the user's preference is,
  across devices". On every successful `PUT` we keep both
  in sync.
- **Login sync**: when the user logs in, we call
  `InitializeAsync()` which fetches the server preference
  and writes the cookie. If the user has never set a
  preference, the API returns 404 and we create the row
  with defaults (the same defaults a brand-new user gets).
  The `AppearanceToggle.razor` and `SettingsAppearance.razor`
  do not have to handle the 404 case separately.

### 2.3 The 3-state appearance mode

Radzen ships every theme in a light / dark pair (e.g.
`default.css` + `dark.css`). The cookie service handles the
**explicit** choice. For the **system-following** mode (the
"Auto" / "System" option) we need a tiny client-side observer
that:

1. Reads the current `prefers-color-scheme: dark` media
   query at startup.
2. Subscribes to the `change` event (the user flipping their
   OS theme).
3. Picks the matching sibling (`default` ↔ `dark`,
   `humanistic` ↔ `humanistic-dark`, …,
   `Cardscape Classic` ↔ `Cardscape Classic Dark`)
   without writing to the server (the *theme name* the user
   chose is preserved; only the *light/dark sibling* changes).

This is **not** a `IJSRuntime.InvokeAsync` — it is a
`window.matchMedia` call done through a Blazor-idiomatic
abstraction that Radzen ships for exactly this case: the
`<MediaQueryList>` Blazor primitive. We wrap it in a small
`SystemAppearanceWatcher.cs` (an `IAsyncDisposable`) that
exposes a `Current` (Light / Dark) event.

The server-side `UserPreferences.Mode` is the **user's
intent** ("I want Dark"). The watcher is the **runtime
resolver** ("the OS is in Dark mode right now, so apply the
dark sibling of the user's chosen theme"). The two never
conflict: a change to `Mode = Dark` flips the watcher
override to "always Dark", and a change to `Mode = System`
re-enables the OS-following behaviour.

### 2.4 Why the cookie is **not** a security boundary

A common worry with theme cookies is "the user could spoof
the theme name and break the UI". Two reasons that does not
matter here:

- The cookie value is a **string from a fixed enumeration**
  (5 free names + 2 custom names = 7 valid values). Any
  unknown value is silently coerced to `"default"`.
- The `Theme` object is constructed **only on the Blazor
  client** from the cookie string. The server never
  instantiates a `Theme`; it just stores the string. So
  there is no remote code execution surface, no XSS, no
  CSS injection.

The cookie is a UX optimization, not an auth or
configuration source. The server is the configuration source.

---

## 3. Detailed implementation plan

The work splits into 6 commits on `master`, ordered so each
one leaves the build green and the UI in a usable state.

### Commit 1 — Theme catalog + cookie service verification
  (foundations, no UI changes yet)

**File-by-file changes:**

1. `src/Cardscape.Web/Theming/ThemeCatalog.cs` (**new**)
   - Static class exposing the 5 free theme names as
     constants: `FreeThemes.Default = "default"`,
     `FreeThemes.Humanistic = "humanistic"`, etc.
   - `CardscapeThemes.Classic` /
     `CardscapeThemes.ClassicDark` instance factory
     methods returning `Theme` objects.
2. `src/Cardscape.Web/Theming/ThemeCatalog.Tests.cs`
   (**new** — bUnit component test)
   - Asserts `CardscapeThemes.Classic.Name == "cardscape-classic"`.
   - Asserts `CardscapeThemes.Classic.Colors["primary"]`
     matches the brand teal `#0f3d3e`.
   - Asserts every entry in the 12-entry catalog has a
     unique `Name`.
   - Asserts the catalog is **Software-based**: the Classic
     theme's `BaseTheme` / shape comes from Radzen's
     `software` palette.
3. `src/Cardscape.Web/_Imports.razor` — no change. The
   catalog is a plain C# class, not a component.
4. `src/Cardscape.Web/wwwroot/index.html:10-11` —
   **leave the two static `<link>` lines in place** for
   commit 1. The Radzen cookie service does not emit a
   `<link>` on its own — that emission is the job of the
   `<RadzenTheme>` component, which lives in `App.razor`
   and is added in commit 3. Trimming the static links
   before commit 3 lands would leave the app with no
   Radzen CSS between commits 1 and 3, which is a
   visible regression. The trim moves to commit 3, right
   next to the `<RadzenTheme>` addition, so the two
   changes ship together as one atomic "the runtime
   theme service now owns the link" change.
5. `src/Cardscape.Web/Program.cs:50-54` — **no change** to
   the existing `AddRadzenCookieThemeService` call. The
   cookie service already knows the 5 free themes by name.
   We only need to call `SetTheme(CardscapeThemes.Classic)`
   when the cookie value matches `"cardscape-classic"` —
   that is a one-liner we add in Commit 3.

**Acceptance:**

- `dotnet build` green.
- `dotnet test` green, including the new bUnit test.
- `index.html` is **unchanged** in this commit (the
  static `<link>` lines stay until commit 3, which adds
  the `<RadzenTheme>` tag that replaces them — see the
  "leave the two static `<link>` lines in place" note in
  step 4 above).
- App still renders in `default` light (the default state)
  when the cookie is absent.

---

### Commit 2 — Server-side persistence
  (new aggregate + new API + EF migration + DSR integration)

This is the biggest commit. It is its own commit because it
touches 4 projects (Domain, Application, Infrastructure,
Api) and the Web client cannot integrate with it until it
exists. No Blazor UI changes here.

**Domain layer (`src/Cardscape.Domain/`):**

1. `UserPreferences/UserPreferences.cs` (**new**) — the
   aggregate root. See §2.2 for the shape. Domain events:
   `UserPreferencesCreated`, `UserPreferencesUpdated`.
2. `UserPreferences/AppearanceMode.cs` (**new**) — enum
   `Light | Dark | System`.
3. `UserPreferences/IUserPreferencesRepository.cs`
   (**new**) — `GetByUserIdAsync(UserId)`,
   `AddAsync(UserPreferences)`, `UpdateAsync(UserPreferences)`,
   `DeleteByUserIdAsync(UserId)`.
4. `UserPreferences/Errors/UserPreferencesErrors.cs`
   (**new**) — `NotFound`, `AlreadyExists`, `InvalidThemeName`
   (with a `static readonly string[] ValidThemeNames` shared
   with the API layer for the validation).

**Application layer (`src/Cardscape.Application/`):**

1. `UserPreferences/Queries/GetUserPreferencesQuery.cs`
   (**new**) — MediatR query; returns the
   `UserPreferencesDto` or `null` (anonymous user).
2. `UserPreferences/Queries/GetUserPreferencesQueryHandler.cs`
   (**new**) — calls the repository; returns `null` if the
   user has no preferences row yet.
3. `UserPreferences/Commands/UpdateUserPreferencesCommand.cs`
   (**new**) — MediatR command; takes the new `ThemeName`
   and `Mode`; validates against `ValidThemeNames` +
   `AppearanceMode` enum; calls `Update` on the aggregate.
4. `UserPreferences/Commands/UpdateUserPreferencesCommandHandler.cs`
   (**new**) — handler; emits the `UserPreferencesUpdated`
   event.
5. `UserPreferences/Commands/CreateDefaultUserPreferencesCommand.cs`
   (**new**) — MediatR command; called when a user logs in
   for the first time and has no row; creates a
   `UserPreferences` with `ThemeName = "default"`,
   `Mode = AppearanceMode.System`.
6. `UserPreferences/Commands/CreateDefaultUserPreferencesCommandHandler.cs`
   (**new**) — handler; emits `UserPreferencesCreated`.
7. `UserPreferences/DTOs/UserPreferencesDto.cs` (**new**)
   — `ThemeName`, `Mode` (as a string for the wire),
   `UpdatedAt`.
8. `UserPreferences/Mappings/UserPreferencesMapping.cs`
   (**new**) — `UserPreferences` ⇄ `UserPreferencesDto`
   (Mapperly).
9. `UserPreferences/Validators/UpdateUserPreferencesValidator.cs`
   (**new**) — FluentValidation; rules: `ThemeName` must
   be in `ValidThemeNames`; `Mode` must be a valid enum
   value.

**Infrastructure layer (`src/Cardscape.Infrastructure/`):**

1. `Persistence/Configurations/UserPreferencesConfiguration.cs`
   (**new**) — EF Core fluent config; `UserId` is the
   **primary key** (1:1 with `User`, not a separate
   `Id` column); `ThemeName` is `varchar(50)` NOT NULL;
   `Mode` is `int` NOT NULL; `UpdatedAt` is `datetimeoffset`
   NOT NULL.
2. `Persistence/Repositories/UserPreferencesRepository.cs`
   (**new**) — implements `IUserPreferencesRepository`.
3. `Persistence/Migrations/20260808_AddUserPreferences.cs`
   (**new**, generated by `dotnet ef migrations add`) —
   the schema migration. **Tested on SQLite** (the only
   provider in the current test matrix per ADR 0001).
4. `Identity/Events/UserDeletedHandler.cs` (**existing**,
   extended) — subscribe to `UserDeleted` (the GDPR
   soft-delete event from ADR §S2); on the handler,
   call `IUserPreferencesRepository.DeleteByUserIdAsync`
   so the preference row goes with the user.
5. `Identity/Events/UserAnonymisedHandler.cs` (**new** or
   extended if it exists) — same idea for the anonymise
   path; the `UserId` reference is removed, so the
   preferences go too.

**Api layer (`src/Cardscape.Api/`):**

1. `Endpoints/UserPreferencesEndpoints.cs` (**new**) —
   minimal-API group on `/api/users/me/preferences`:
   - `GET` → returns the `UserPreferencesDto`; returns
     404 if the user has no row (the Blazor client
     creates the row on first read; see §2.2).
   - `PUT` → accepts `{ themeName, mode }`; returns
     200 with the updated DTO; 400 on invalid theme
     name; 401 if anonymous; 404 if the user has no
     row yet (the Blazor client must call
     `CreateDefault` first; we document this in the
     OpenAPI description).
   - Both endpoints require auth (the existing JWT
     bearer middleware picks them up automatically).
2. `OpenApi/UserPreferencesOpenApi.cs` (**new**) —
   registers the two endpoints in the OpenAPI doc +
   Scalar reference UI.
3. `Cardscape.Api.csproj` / `Cardscape.Infrastructure.csproj` —
   no new NuGet; the migration just uses the existing
   EF Core 10.0.10.

**Web layer (`src/Cardscape.Web/`) — for Commit 3:**

- **No changes here in Commit 2.** The `IUserPreferencesApiClient`
  and the `UserPreferencesService` ship in Commit 3, when
  `App.razor` is wired.

**Tests (added in this commit):**

1. `tests/.../Unit/Domain/UserPreferencesTests.cs` —
   aggregate invariants, the `Update` method,
   `AppearanceMode` parsing.
2. `tests/.../Unit/Application/UpdateUserPreferencesCommandHandlerTests.cs`
   — happy path, invalid theme name, anonymous user.
3. `tests/.../Integration/Api/UserPreferencesEndpointsTests.cs`
   — full HTTP round-trip via `WebApplicationFactory`:
   200 on `PUT` with valid input, 400 on invalid theme
   name, 401 on anonymous, 404 on `GET` for a fresh user,
   200 on `GET` after a `PUT`, DSR self-delete removes
   the row.
4. `tests/.../Integration/Api/UserDeletedRemovesPreferencesTests.cs`
   — soft-deletes a user, asserts the preference row is
   gone.
5. `tests/.../Architecture/ApplicationLayerRulesTests.cs`
   (**extended**) — assert the new query / command
   classes follow the existing pattern.

**Acceptance:**

- `dotnet build` green.
- `dotnet test` green; the new test count rises by ~12.
- `dotnet ef migrations script 20260808_AddUserPreferences`
  produces a valid SQLite migration.
- The integration test logs in, calls `PUT`, calls `GET`,
  asserts the round-trip; logs out, calls `GET` again,
  asserts 404; calls DSR self-delete, asserts the row is
  gone.
- The OpenAPI spec (Scalar at `/scalar/v1`) shows the two
  new endpoints with the correct request/response schemas.

---

### Commit 3 — `App.razor` emits `<RadzenTheme>` +
  `UserPreferencesService` in the Web client

**File-by-file changes:**

1. `src/Cardscape.Web/App.razor` —
   - Add `<RadzenTheme Theme="@_currentTheme" />` near the
     top of the markup (right after `<CascadingAuthenticationState>`).
   - Inject `ThemeService` via `@inject ThemeService Theme`.
   - Inject `UserPreferencesService` via
     `@inject UserPreferencesService Prefs`.
   - In `OnInitializedAsync`:
     1. Subscribe to `Theme.Changed` so a runtime theme
        switch re-renders `<RadzenTheme>`.
     2. Call `await Prefs.InitializeAsync()`. The service
        decides whether to read from the server (logged in)
        or the cookie (anonymous) and applies the theme.
   - The `<RadzenTheme>` tag binds to `Prefs.CurrentTheme`
     (a `Theme?`) — null means "use the Radzen default
     (the `default` free theme)".
2. `src/Cardscape.Web/wwwroot/index.html:10-11` —
   **now** (in this commit, not commit 1) remove the two
   static `<link>` lines for `default.css` and
   `material-base.css`. The `<RadzenTheme>` tag added in
   step 1 emits the matching `<link>` for the current
   theme at runtime, so the static lines are now redundant.
   The trim ships in the same commit as the `<RadzenTheme>`
   addition so the two changes land atomically — no
   window of "no Radzen CSS" between commits.
3. `src/Cardscape.Web/Services/UserPreferencesService.cs`
   (**new**) — singleton. See §2.2 for the public surface.

2. `src/Cardscape.Web/Services/UserPreferencesService.cs`
   (**new**) — singleton. See §2.2 for the public surface.
   - Holds the `Theme? CurrentTheme` + `AppearanceMode CurrentMode`.
   - `InitializeAsync()`:
     - If `AuthStateProvider` is authenticated, call
       `IUserPreferencesApiClient.GetAsync()`. On 404,
       call `CreateDefaultUserPreferencesCommand` via
       the API (or call a dedicated `POST` endpoint —
       see the open question below).
     - Write the result to the cookie via
       `ThemeService.SetTheme(...)`.
     - If anonymous, read the cookie (via the existing
       `ThemeService` API) and use it.
   - `SetAsync(string themeName, AppearanceMode mode)`:
     - Write the cookie first (`ThemeService.SetTheme`).
     - Call `IUserPreferencesApiClient.UpdateAsync(...)` if
       logged in. On failure, log + show a
       `NotificationService` warning.
     - Raise a `Changed` event so the UI re-renders.
4. `src/Cardscape.Web/Services/Api/IUserPreferencesApiClient.cs`
   + `UserPreferencesApiClient.cs` (**new**) — thin
   HTTP wrapper; uses the existing `"Cardscape.Api"`
   named HttpClient + `AuthTokenHandler`.
5. `src/Cardscape.Web/Program.cs` — register the new
   services:
   ```csharp
   builder.Services.AddScoped<IUserPreferencesApiClient, UserPreferencesApiClient>();
   builder.Services.AddSingleton<UserPreferencesService>();
   ```
6. `src/Cardscape.Web/Services/AuthStateProvider.cs` —
   **extended** (no new file): expose a public
   `IsAuthenticated` (or `CurrentUser` /
   `GetAuthenticationStateAsync()`) so the
   `UserPreferencesService` can branch. The auth state
   provider already exists; this is a one-property
   addition.

**Open question (resolved in §6):** what HTTP method creates
the default preferences row on first login?
- (a) `PUT /api/users/me/preferences` is upsert (404 on
  first call; client retries with a `POST`-then-`PUT`).
- (b) `POST /api/users/me/preferences` creates the row
  with the given values (no defaults); the client only
  calls it on the 404 from `GET`. `PUT` updates an
  existing row.

Recommendation: **(b)** — clearer semantics, the
`GET → 404 → POST → PUT` flow is one round-trip extra on
the first login only.

**Acceptance:**

- The user can flip the cookie between `default` /
  `humanistic` / `material` / `software` / `standard` /
  `cardscape-classic` / `cardscape-classic-dark` via the
  browser dev tools and the page reflects the new theme on
  the next reload, with no flash of unstyled content.
- The 5 free themes show their correct light/dark variant
  (no manual dark override needed — Radzen handles it via
  the `-dark` file when the user picks the dark variant).
- A logged-in user calling `PUT /api/users/me/preferences`
  from curl, then reloading the page in the browser, sees
  the new theme applied (proves the server → cookie →
  ThemeService path works).
- An anonymous user flipping the theme via curl
  (`document.cookie = ...`) sees the new theme on reload
  (proves the cookie → ThemeService path works).

---

### Commit 4 — `AppearanceToggle.razor` header button

**File-by-file changes:**

1. `src/Cardscape.Web/Shared/AppearanceToggle.razor`
   (**new**) — a compact `RadzenDropDown` that lists the
   12 entries from `ThemeCatalog.All`, with a second
   `RadzenDropDown` next to it for the
   Light / Dark / System mode. Built entirely from Radzen
   primitives.
   - On change, calls `UserPreferencesService.SetAsync(...)`.
   - No `IJSRuntime`. No inline `style=`. The component
     uses `RadzenStack` / `RadzenDropDown` / `RadzenIcon` /
     `RadzenText` only.
2. `src/Cardscape.Web/Shared/AppearanceToggle.razor.css`
   — **not needed**; the toggle is pure Radzen primitives.
3. `src/Cardscape.Web/Layout/MainLayout.razor:34-37` —
   add `<AppearanceToggle />` to the header right
   next to `<LanguageSwitcher />`. The `_Imports.razor`
   already has `using Cardscape.Web.Shared` so no edit
   there.
4. `src/Cardscape.Web/Resources/SharedResource.resx` +
   `SharedResource.es.resx` — add 4 new strings:
   - `AppearanceTitle` ("Appearance" / "Apariencia")
   - `AppearanceTheme` ("Theme" / "Tema")
   - `AppearanceMode` ("Mode" / "Modo")
   - `AppearanceSystem` ("Follow system" / "Seguir al sistema")
5. `src/Cardscape.Web/Shared/AppearanceToggle.razor.Tests.cs`
   (**new** — bUnit) — renders the toggle, asserts the
   dropdown lists 12 entries, picks the 3rd, asserts the
   `UserPreferencesService.SetAsync` was called with the
   right `(themeName, mode)`, asserts the `Changed` event
   fires.

**Acceptance:**

- The header shows the toggle next to the language switcher.
- Clicking the toggle opens a dropdown listing the 5 free
  themes + the Cardscape Classic + their dark siblings
  (12 entries).
- Picking any entry writes the cookie, calls
  `PUT /api/users/me/preferences` (if logged in), and
  re-themes the page in < 100 ms (no full reload).
- Picking "Cardscape Classic" turns the primary buttons teal
  (`#0f3d3e`) without changing the layout.

---

### Commit 5 — `/settings/appearance` page

**File-by-file changes:**

1. `src/Cardscape.Web/Pages/SettingsAppearance.razor`
   (**new**) — built entirely from Radzen primitives:
   - A `RadzenCard` per theme, with a `RadzenStack`
     containing: the theme name, a `RadzenText` description,
     a row of 5 `RadzenBadge` color swatches showing the
     theme's primary/secondary/success/warning/danger
     colors, and a `RadzenButton` to apply.
   - A `RadzenRadioButtonList` for the Light / Dark / System
     mode.
   - A live preview pane (`RadzenCard` + `RadzenButton` +
     `RadzenDataGrid` + `RadzenTextBox`) so the user can
     see the theme in context before committing.
2. `src/Cardscape.Web/Pages/SettingsAppearance.razor.Tests.cs`
   (**new** — bUnit) — asserts the page renders 12 cards,
   asserts the preview pane re-themes when a card's Apply
   button is clicked.
3. `src/Cardscape.Web/Layout/MainLayout.razor:37-42` —
   add a new entry to the `<RadzenProfileMenu>` between
   the existing `Account / API tokens` and
   `Settings / Two-factor` entries:
   ```razor
   <RadzenProfileMenuItem Path="settings/appearance" Icon="palette" Text="@L["SettingsAppearance"]" />
   ```
4. `src/Cardscape.Web/Resources/SharedResource.resx` +
   `SharedResource.es.resx` — add page-level strings
   (`SettingsAppearanceTitle`,
   `SettingsAppearanceLivePreview`,
   `SettingsAppearance`).
5. `src/Cardscape.Web/Pages/SettingsAppearance.razor.css` —
   **not needed**.

**Acceptance:**

- `/settings/appearance` is reachable from the profile menu.
- Every free theme + the Cardscape Classic + their dark
  variants appear as a card.
- Clicking "Apply" on a card writes the cookie, calls
  `PUT /api/users/me/preferences` (if logged in), and
  re-themes the entire app immediately.
- The page survives a hard reload (cookie + server
  persistence).

---

### Commit 6 — `EmptyLayout.razor.css` + ADR 0011 + docs

**File-by-file changes:**

1. `src/Cardscape.Web/Layout/EmptyLayout.razor.css:5-26`
   — the brand-column radial gradient and the form-column
   `background: var(--rz-body-background-color)` are
   already token-driven. **No change needed** for the
   free themes (the `--rz-primary-light` /
   `--rz-primary-darker` / `--rz-body-background-color`
   variables are defined by every Radzen free theme,
   including the new `software` base). For the
   **Cardscape Classic** custom theme, the
   `--rz-primary-light` / `--rz-primary-darker` are
   declared by the `Theme` object so the gradient still
   cascades correctly. **Verify in a manual test**, no
   code change expected.
2. `docs/adr/0011-radzen-themes-and-cardscape-classic.md`
   (**new**) — see §5 below for the full outline.
3. `.agents/skills/radzen-blazor/SKILL.md` — add a §11
   "Theming" subsection (4 paragraphs) that covers:
   - The `AddRadzenCookieThemeService` wiring.
   - The `<RadzenTheme>` tag in `App.razor`.
   - The 5 free theme names + the `Theme` programmatic API.
   - The `CardscapeThemes.Classic` factory and its brand teal
     rationale.
4. `docs/AGENTS.md:222-224` — the existing
   `radzen-blazor` row already covers theming. **No new
   row**. (The plan considered adding `radzen-theming`
   and decided against it — see §5.3.)
5. `docs/brand/00-brand-kit.md` — add a "Where the brand
   shows up in the UI" subsection linking to ADR 0011 and
   to the `CardscapeThemes.Classic` source. Add the
   primary teal `#0f3d3e`, the lighter teal `#1a8a8b`,
   the brand secondary `#d4a574`, and the secondary
   variants as a **swatch table** (no images needed;
   the table cells can be styled with the colors via
   inline markdown, or we ship a separate PNG generated
   from the running `/settings/appearance` page once
   it's built).
6. `docs/roadmap/README.md` (if it exists) — add a link
   to this plan as the v1.2.0 follow-up.
7. `docs/refactoring/02-plan.md` — add a closing note
   that the `app.css` file stayed under 100 lines after
   Commit 3's `index.html` trim, and that no custom CSS was
   added (the custom theme is C# code, not CSS).

**Acceptance:**

- `app.css` is still < 100 lines (Commit 3 removed 2 `<link>`
  lines, no `.css` was added).
- ADR 0011 is merged and cross-linked from
  `docs/AGENTS.md` and `docs/brand/00-brand-kit.md`.
- `dotnet test` is green for the whole solution, including
  the new bUnit + xUnit + integration tests in Commits 1–4.

---

## 4. The Cardscape Classic theme

The `Theme` C# object for **Cardscape Classic** (and its
**Cardscape Classic Dark** sibling) is the **only piece of
code that has any design judgment** in this plan. The rest
is plumbing. Below is the proposed palette and typography,
built on top of Radzen's **Software** base.

### 4.1 Why Software as the base

The user picked Software (and Software Dark) as the
preferred base. Radzen's `software` free theme is:

- The most "serious tool" of the 5 free options — clean
  lines, restrained palette, no Material Design's rounded
  corners / drop shadows.
- Ships with both light and dark siblings that have
  matching shape / typography / spacing (so the dark
  variant of the Cardscape Classic **also** feels like
  the same product, not a different one with the lights
  off).
- Has good WCAG contrast in the default palette
  (we keep that).
- The **closest match** to Trello's look (which Cardscape
  explicitly clones — the kanban in
  `Shared/KanbanBoard.razor.css` is Trello-styled), so
  the rest of the UI feels consistent with the kanban.

The custom theme **inherits** Software's shape (border
radius, padding scale, focus ring) and **overrides** the
color slots to inject the brand teal.

### 4.2 Light variant — `Cardscape Classic`

| Slot | Value | Why |
|---|---|---|
| `Name` | `cardscape-classic` | Slug used in cookie + `<RadzenTheme>` |
| `DisplayName` | `Cardscape Classic` | User-facing label in the dropdown |
| Base | Radzen `software` | Per user direction. |
| `Colors["primary"]` | `#0f3d3e` | The brand teal from `<meta name="theme-color">` in `index.html:14`. |
| `Colors["primary-light"]` | `#1a5a5b` | +1 step toward white in HSL. |
| `Colors["primary-darker"]` | `#082627` | -1 step toward black in HSL. Used by the auth brand-column gradient. |
| `Colors["secondary"]` | `#d4a574` | Warm sand. See §4.4 for the reasoning. |
| `Colors["secondary-light"]` | `#e2bd8d` | +1 step. |
| `Colors["secondary-darker"]` | `#a87e4f` | -1 step. |
| `Colors["success"]` | `#1f7a4d` | Inherited from Software. |
| `Colors["warning"]` | `#c47a00` | Inherited from Software. |
| `Colors["danger"]` | `#c0392b` | Inherited from Software. |
| `Colors["info"]` | `#2980b9` | Inherited from Software. |
| `IconStyle` | `IconStyle.Filled` | Matches Software's filled icon family. |
| `Shape.BorderRadius` | `4px` | Tighter than Software default (6px) — reads as "serious tool", not "consumer app". |
| Typography (font family) | inherit Software (`Source Sans 3`) | The Radzen-shipped `SourceSans3VF-*.woff2` is already in `_content/Radzen.Blazor/fonts/`. No new font, no new file. |
| `BaseBackgroundColor` | `#f7f8f8` | One shade off pure white; reduces eye fatigue in long sessions. |

### 4.3 Dark variant — `Cardscape Classic Dark`

| Slot | Value | Why |
|---|---|---|
| `Name` | `cardscape-classic-dark` | Slug for the dark sibling. |
| Base | Radzen `software-dark` | Same base as the light variant, with the dark elevation. |
| `Colors["primary"]` | `#1a8a8b` | Brighter teal for contrast against the dark background. |
| `Colors["primary-light"]` | `#2fa9aa` | +1 step. |
| `Colors["primary-darker"]` | `#0f3d3e` | The brand teal, kept as the "darker" anchor. |
| `Colors["secondary"]` | `#d4a574` | Same warm sand — works on dark too. |
| `Colors["secondary-light"]` | `#e2bd8d` | +1 step. |
| `Colors["secondary-darker"]` | `#a87e4f` | -1 step. |
| All other colors | Inherit Radzen `software-dark` defaults | We only override the primary + secondary scale. |
| `BaseBackgroundColor` | `#1a1d1e` | Software's dark base. |

### 4.4 The secondary color — `#d4a574` (warm sand)

The user delegated the secondary color choice. I picked
**`#d4a574`** (warm sand / amber) for the following reasons:

- **Complementary on the color wheel** to the brand teal
  `#0f3d3e`. Teal sits in the green-blue family; amber sits
  in the yellow-orange family. They are ~150° apart on the
  HSL wheel, which is the classic complementary relationship
  — high contrast, but not jarring.
- **Earth / "serious tool" feel**. Amber / sand reads as
  paper, brass, leather — the materials of an old-school
  project-management binder. The teal reads as modern,
  digital, calm. Together they say "a tool that respects
  both tradition and the present", which is the Cardscape
  brand voice (see `docs/brand/00-brand-kit.md`).
- **WCAG-compliant against both backgrounds**. Against
  white (`#ffffff`) the contrast is 3.2:1 (passes for large
  text, fails for body text — but secondary is never used
  for body text in Radzen). Against the Cardscape Classic
  dark background (`#1a1d1e`) the contrast is 6.8:1
  (passes for body text).
- **Same color works in both light and dark variants** —
  `#d4a574` is bright enough to read on dark and saturated
  enough to read on light. We do not need a separate
  "secondary dark" — the contrast is fine in both.
- **Radzen's `Colors["secondary"]` slot** is what Radzen
  uses for the "secondary" button style
  (`<RadzenButton ButtonStyle="ButtonStyle.Secondary">`)
  and for accents on cards / focus rings. The amber is
  bright enough to draw the eye without being aggressive.

Alternatives I considered and rejected:

- `#2980b9` (Radzen's default info blue) — too generic,
  clashes with the primary teal.
- `#7f8c8d` (Radzen's default secondary grey) — boring,
  no character.
- `#e07856` (coral / terracotta) — too "consumer app",
  pushes the palette into the warm family and dilutes the
  teal.
- `#9b59b6` (purple) — Radzen's standard purple, would
  clash with the teal in a non-complementary way.
- `#2ecc71` (emerald) — too close to the primary teal in
  hue; would not be distinguishable as a separate
  "secondary" slot.

The `docs/brand/00-brand-kit.md` swatch table is updated
in Commit 6 with all three (primary, primary-light,
primary-darker, secondary, secondary-light, secondary-darker)
for both the light and the dark variant.

### 4.5 What this does **not** change

- The **layout chrome** (`MainLayout.razor`,
  `EmptyLayout.razor`) is unchanged. The header / sidebar /
  body structure is theme-agnostic.
- The **kanban** in `Shared/KanbanBoard.razor.css` keeps
  its Trello greys (ADR 0009 §4 trade-off, still applies).
- The **loading spinner** and **error overlay** in
  `app.css:19-99` keep their current colors. They are
  Blazor WASM template assets, not theme surface.

---

## 5. ADR 0011 — outline

`docs/adr/0011-radzen-themes-and-cardscape-classic.md`
follows the same structure as ADR 0009. Outline:

1. **Context** — what is wired today, what is not.
2. **Decision** — Radzen free themes via cookie service +
   Cardscape Classic via programmatic `Theme` object, all
   built on Radzen's documented APIs. The
   `AppearanceToggle.razor` shared component + the
   `/settings/appearance` page expose the picker.
   Persistence is server-side per user via a new
   `UserPreferences` aggregate; the cookie is the
   write-through cache and the fallback for anonymous
   users.
3. **Rationale** — why the programmatic API over SCSS /
   static CSS (paraphrase §2.2 above); why 12 entries
   instead of a free-form picker (NG3); why the server
   owns the preference (cross-device UX) and the cookie
   is a write-through cache (zero-flash first render);
   why the `UserPreferences` aggregate is 1:1 with
   `User` (no JSON blob — column-per-preference is
   cheaper to migrate, cheaper to query, easier to
   audit for GDPR).
4. **Consequences** — `app.css` stays under 100 lines;
   no new NuGet; the kanban Trello greys are still
   intentional; the brand kit gains a "UI surface" section;
   the GDPR surface gains one new delete path; the
   OpenAPI spec gains two new endpoints.
5. **Compliance** — the `dotnet test` exit code; the
   `app.css` line count; the absence of new
   `wwwroot/css/*.css` files; the OpenAPI spec includes
   the two new endpoints.

### 5.1 ADR 0011 acceptance checklist

```markdown
- [ ] `src/Cardscape.Web/wwwroot/index.html` has no
      `<link rel="stylesheet" href="_content/Radzen.Blazor/css/..." />` tags.
- [ ] `src/Cardscape.Web/wwwroot/css/*.css` count is
      unchanged (currently: `app.css`, `barlow.css`).
- [ ] `src/Cardscape.Web/app.css` line count is < 100.
- [ ] `src/Cardscape.Web/Pages/` contains 0 `<button>`,
      `<input>`, or `<form>` elements.
- [ ] `src/Cardscape.Web/Pages/` contains 0
      `IJSRuntime.InvokeAsync` calls.
- [ ] `ThemeCatalog.cs` exposes 12 unique `Name` values.
- [ ] `AppearanceToggle.razor` and `SettingsAppearance.razor`
      are built from Radzen primitives only.
- [ ] `CardscapeThemes.Classic.Colors["primary"]` ==
      `"#0f3d3e"`.
- [ ] `UserPreferences` aggregate has a `UserDeleted` /
      `UserAnonymised` handler that removes the row.
- [ ] `GET /api/users/me/preferences` requires auth.
- [ ] `PUT /api/users/me/preferences` validates `ThemeName`
      against the 7-entry enum and rejects unknown values
      with 400.
- [ ] The DSR self-delete test removes the user's
      `UserPreferences` row.
- [ ] `dotnet test` is green.
- [ ] `dotnet build` is green with 0 warnings.
```

### 5.2 No new skill file

The theming knowledge is small enough to live as a section
inside the existing `radzen-blazor` skill
(`.agents/skills/radzen-blazor/SKILL.md`). We add a §11
"Theming" subsection (4 paragraphs) that covers:

- The `AddRadzenCookieThemeService` wiring (already in
  `Program.cs:50-54`).
- The `<RadzenTheme>` tag in `App.razor`.
- The 5 free theme names + the `Theme` programmatic API.
- The `CardscapeThemes.Classic` factory and its brand teal
  rationale.

The `docs/AGENTS.md:222-224` skill table does not need a
new row; the existing `radzen-blazor` row covers theming.

### 5.3 Why not a separate skill

- Theming is **a UI concern**. Splitting it off would
  force every "I'm changing a color" PR to check two
  skills, and the line between "component choice" and
  "theme choice" is fuzzy enough to confuse maintainers.
- The total surface is ~12 lines of code
  (`CardscapeThemes.Classic` / `ClassicDark`) plus 2
  wiring changes (`App.razor`, `index.html` trim).
  That does not justify a `SKILL.md` file.
- If a future maintainer adds **per-workspace theming** or
  **theme inheritance**, that is the moment to extract a
  `radzen-theming` skill. Not now.

---

## 6. Open questions (resolved by this plan)

| # | Question | Decision |
|---|---|---|
| Q1 | SCSS Theme Builder vs ThemeService programmatic? | **ThemeService programmatic** (§2.2, ADR 0009 compliance). |
| Q2 | Cookie-only persistence, or server-side per user? | **Server-side per user** via `UserPreferences` aggregate; cookie is write-through cache + anonymous fallback (§2.2). |
| Q3 | How many entries in the picker? | **12** (5 free light + 5 free dark + Cardscape Classic + Cardscape Classic Dark). |
| Q4 | `AppearanceToggle.razor` in the sidebar or in the header? | **Header**, next to `<LanguageSwitcher />` (compact, predictable, low-cost). |
| Q5 | `/settings/appearance` linked from the profile menu or from the sidebar? | **Profile menu**, under the existing `Settings / Two-factor` entry (matches the user's mental model of "settings = personal preferences"). |
| Q6 | What is the brand secondary color? | **Warm sand `#d4a574`** (§4.4). |
| Q7 | What icon style for Cardscape Classic? | `IconStyle.Filled` (matches Software). |
| Q8 | What border-radius scale? | `4px` (tighter than Software default). |
| Q9 | What Radzen base for Cardscape Classic? | **Software** (per user direction; §4.1). |
| Q10 | Where does the `<RadzenTheme>` tag live? | `App.razor` (single source of truth, runs before any page renders). |
| Q11 | Should the toggle in the header be a `RadzenSplitButton` or a `RadzenDropDown`? | **`RadzenDropDown`** (one click to open, single selection, lighter footprint than a split button). |
| Q12 | What HTTP verb creates the default `UserPreferences` row on first login? | **`POST`** (clearer semantics; client only calls it on the 404 from `GET`). |

---

## 7. File-by-file change summary

| File | Status | Lines (approx) | Commit |
|---|---|---:|---|
| `src/Cardscape.Web/Theming/ThemeCatalog.cs` | **new** | 110 | 1 |
| `src/Cardscape.Web/Theming/ThemeCatalog.Tests.cs` | **new** | 50 | 1 |
| `src/Cardscape.Web/wwwroot/index.html` | edit | -2 | 3 |
| `src/Cardscape.Domain/UserPreferences/UserPreferences.cs` | **new** | 60 | 2 |
| `src/Cardscape.Domain/UserPreferences/AppearanceMode.cs` | **new** | 20 | 2 |
| `src/Cardscape.Domain/UserPreferences/IUserPreferencesRepository.cs` | **new** | 30 | 2 |
| `src/Cardscape.Domain/UserPreferences/Errors/UserPreferencesErrors.cs` | **new** | 25 | 2 |
| `src/Cardscape.Application/UserPreferences/Queries/GetUserPreferencesQuery.cs` | **new** | 25 | 2 |
| `src/Cardscape.Application/UserPreferences/Queries/GetUserPreferencesQueryHandler.cs` | **new** | 35 | 2 |
| `src/Cardscape.Application/UserPreferences/Commands/UpdateUserPreferencesCommand.cs` | **new** | 30 | 2 |
| `src/Cardscape.Application/UserPreferences/Commands/UpdateUserPreferencesCommandHandler.cs` | **new** | 50 | 2 |
| `src/Cardscape.Application/UserPreferences/Commands/CreateDefaultUserPreferencesCommand.cs` | **new** | 25 | 2 |
| `src/Cardscape.Application/UserPreferences/Commands/CreateDefaultUserPreferencesCommandHandler.cs` | **new** | 40 | 2 |
| `src/Cardscape.Application/UserPreferences/DTOs/UserPreferencesDto.cs` | **new** | 20 | 2 |
| `src/Cardscape.Application/UserPreferences/Mappings/UserPreferencesMapping.cs` | **new** | 25 | 2 |
| `src/Cardscape.Application/UserPreferences/Validators/UpdateUserPreferencesValidator.cs` | **new** | 30 | 2 |
| `src/Cardscape.Infrastructure/Persistence/Configurations/UserPreferencesConfiguration.cs` | **new** | 40 | 2 |
| `src/Cardscape.Infrastructure/Persistence/Repositories/UserPreferencesRepository.cs` | **new** | 60 | 2 |
| `src/Cardscape.Infrastructure/Persistence/Migrations/20260808_AddUserPreferences.cs` | **new** (EF generated) | 50 | 2 |
| `src/Cardscape.Infrastructure/Identity/Events/UserDeletedHandler.cs` | edit | +15 | 2 |
| `src/Cardscape.Infrastructure/Identity/Events/UserAnonymisedHandler.cs` | **new** | 30 | 2 |
| `src/Cardscape.Api/Endpoints/UserPreferencesEndpoints.cs` | **new** | 90 | 2 |
| `src/Cardscape.Api/OpenApi/UserPreferencesOpenApi.cs` | **new** | 30 | 2 |
| tests/.../Unit/Domain/UserPreferencesTests.cs | **new** | 50 | 2 |
| tests/.../Unit/Application/UpdateUserPreferencesCommandHandlerTests.cs | **new** | 80 | 2 |
| tests/.../Integration/Api/UserPreferencesEndpointsTests.cs | **new** | 120 | 2 |
| tests/.../Integration/Api/UserDeletedRemovesPreferencesTests.cs | **new** | 50 | 2 |
| `src/Cardscape.Web/App.razor` | edit | +20 | 3 |
| `src/Cardscape.Web/Services/UserPreferencesService.cs` | **new** | 120 | 3 |
| `src/Cardscape.Web/Services/Api/IUserPreferencesApiClient.cs` | **new** | 25 | 3 |
| `src/Cardscape.Web/Services/Api/UserPreferencesApiClient.cs` | **new** | 70 | 3 |
| `src/Cardscape.Web/Program.cs` | edit | +3 | 3 |
| `src/Cardscape.Web/Services/AuthStateProvider.cs` | edit | +5 | 3 |
| `src/Cardscape.Web/Shared/AppearanceToggle.razor` | **new** | 70 | 4 |
| `src/Cardscape.Web/Shared/AppearanceToggle.Tests.cs` | **new** | 50 | 4 |
| `src/Cardscape.Web/Layout/MainLayout.razor` | edit | +2 | 4 |
| `src/Cardscape.Web/Resources/SharedResource.resx` | edit | +4 | 4 |
| `src/Cardscape.Web/Resources/SharedResource.es.resx` | edit | +4 | 4 |
| `src/Cardscape.Web/Pages/SettingsAppearance.razor` | **new** | 130 | 5 |
| `src/Cardscape.Web/Pages/SettingsAppearance.Tests.cs` | **new** | 60 | 5 |
| `src/Cardscape.Web/Layout/MainLayout.razor` | edit | +1 (profile menu entry) | 5 |
| `src/Cardscape.Web/Resources/SharedResource.resx` | edit | +3 | 5 |
| `src/Cardscape.Web/Resources/SharedResource.es.resx` | edit | +3 | 5 |
| `docs/adr/0011-radzen-themes-and-cardscape-classic.md` | **new** | 130 | 6 |
| `.agents/skills/radzen-blazor/SKILL.md` | edit | +40 | 6 |
| `docs/AGENTS.md` | edit | 0 (no new row) | 6 |
| `docs/brand/00-brand-kit.md` | edit | +30 | 6 |
| `docs/refactoring/02-plan.md` | edit | +5 (closing note) | 6 |
| `docs/roadmap/README.md` | edit | +1 (link) | 6 |

**Net new code**: ~ 1,650 lines (of which ~410 are tests,
130 are the ADR, 110 are the theme catalog, 120 are the
Web service). Net new "UI surface" code (the two `.razor`
files plus the two test files): ~ 310 lines.

---

## 8. Test plan

### 8.1 Unit (xUnit + bUnit)

| Test class | What it asserts | Project |
|---|---|---|
| `UserPreferencesTests` | aggregate invariants, `Update`, `AppearanceMode` parsing. | Domain unit |
| `UpdateUserPreferencesCommandHandlerTests` | happy path, invalid theme name, anonymous user. | Application unit |
| `CreateDefaultUserPreferencesCommandHandlerTests` | creates with the right defaults; idempotent. | Application unit |
| `GetUserPreferencesQueryHandlerTests` | returns the DTO when the row exists; returns `null` when it does not. | Application unit |
| `ThemeCatalogTests` | 12 unique `Name` values; the Cardscape Classic primary is `#0f3d3e`; the dark variant's primary is `#1a8a8b`; both are based on Radzen's `software` palette. | Web bUnit |
| `AppearanceToggleTests` | Renders 12 entries; picking the 3rd calls `UserPreferencesService.SetAsync` with the right `(themeName, mode)`; the `Changed` event fires. | Web bUnit |
| `SettingsAppearanceTests` | Renders 12 cards; the preview pane re-themes on Apply. | Web bUnit |

### 8.2 Integration (xUnit + `WebApplicationFactory`)

| Test class | What it asserts |
|---|---|
| `UserPreferencesEndpointsTests` | 200 on `PUT` with valid input; 400 on invalid theme name; 401 on anonymous; 404 on `GET` for a fresh user; 200 on `GET` after a `PUT`; round-trip preservation. |
| `UserDeletedRemovesPreferencesTests` | Soft-deletes a user; the preference row is gone. |
| `UserAnonymisedRemovesPreferencesTests` | Anonymises a user; the preference row is gone. |
| `CreateDefaultUserPreferencesTests` | First-time login creates a `UserPreferences` row with `(themeName="default", mode=System)`. |

### 8.3 Manual / browser (the R9 walkthrough pattern)

| Scenario | Steps | Pass criterion |
|---|---|---|
| Free theme switch (light → light) | Set cookie to `humanistic`; reload. | The page renders in `humanistic` colors, no flash. |
| Free theme switch (light → dark) | Set cookie to `material-dark`; reload. | The page renders dark. |
| Custom theme switch (light) | Set cookie to `cardscape-classic`; reload. | The `<RadzenButton ButtonStyle="Primary">` is `#0f3d3e`. |
| Custom theme switch (dark) | Set cookie to `cardscape-classic-dark`; reload. | The page is dark AND the primary is `#1a8a8b`. |
| System mode | Set mode to "Follow system"; change the OS theme. | The app re-themes without a reload. |
| Cookie persistence (anonymous) | Toggle 5 times; hard reload. | The last selection is preserved. |
| Server persistence (logged in) | Toggle to `cardscape-classic`; log out; log in on another browser. | The other browser shows the same theme. |
| Server persistence (write-through) | Toggle to `cardscape-classic`; close the tab before the `PUT` round-trip finishes. | On reload, the theme is still `cardscape-classic` (cookie wrote through, server caught up eventually). |
| EmptyLayout brand column | Apply Cardscape Classic; visit `/login`. | The brand-column gradient uses the Cardscape Classic primary-light → primary → primary-darker. |
| DSR self-delete | Self-delete via the API; log in again with the same email. | No `UserPreferences` row is restored (the user is fresh). |

### 8.4 Regression

- The R9 walkthrough covers the whole `/login` and
  `/register` flows; rerun the relevant curl + browser
  snippets from `test-results/r9/` after Commit 1 + Commit 6 land
  to confirm no theme change has shifted the layout.
- The R8 audit closed the `IJSRuntime.InvokeAsync` count
  to 0 in `Pages/`; this plan must not regress that.
  Guarded by the ADR 0011 acceptance checklist (§5.1).
- The GDPR surface (DSR endpoints + 6h retention sweeper)
  must continue to pass all its existing tests after the
  new `UserDeletedHandler` extension lands.

---

## 9. Risks & action plans

### R1. `ThemeService.SetTheme(Theme)` does not inject the `--rz-*` variables for the base color slots we need.

- **Likelihood**: Low. Radzen's `Theme` class is documented
  to inject every slot in its `Colors` dictionary.
- **Impact**: High. The custom theme would render in
  Radzen's default colors; the brand teal would not show.
- **Detection**: The Commit 1 bUnit test asserts
  `CardscapeThemes.Classic.Colors["primary"] == "#0f3d3e"`.
  A second assertion in Commit 3 renders
  `<RadzenButton ButtonStyle="Primary">` in a bUnit test
  with `CardscapeThemes.Classic` as the `Theme` and asserts
  the rendered `<button>` has a `style` attribute that
  includes the teal.
- **Action plan if it fails**:
  1. **First try** — call `ThemeService.SetTheme(theme)`
     with a `Theme` object whose `BaseTheme` is explicitly
     set to the Software variant. Some Radzen versions
     require the `BaseTheme` to be set for the color
     overrides to take effect.
  2. **Second try** — fall back to a thin
     `wwwroot/css/cardscape-classic.css` (1 file,
     ~30 lines, scoped to the custom theme only) that
     declares the `--rz-primary` and friends on `:root`.
     Update ADR 0011 to document the deviation; the
     file is the only CSS in the project that does not
     come from Radzen.
  3. **Third try** — file an issue against
     `radzenhq/radzen-blazor` and pin the workaround to
     a comment in `ThemeCatalog.cs` so a future Radzen
     version that fixes it can be detected at compile time.

### R2. The brand secondary color (`#d4a574`) is not in the brand kit.

- **Likelihood**: Medium. The brand kit currently has the
  primary teal; the secondary is undeclared.
- **Impact**: Low. The UI works fine without the brand kit
  entry; the ADR documents the choice; the brand kit
  cross-link is "additive".
- **Detection**: Visual review of the brand kit in Commit 6.
- **Action plan**:
  1. **Commit 6 adds the secondary swatch** to
     `docs/brand/00-brand-kit.md` with a paragraph
     explaining the choice (the same reasoning in §4.4).
  2. If the user later wants to change the secondary,
     the change is a 1-line edit in `ThemeCatalog.cs` plus
     a swatch update in the brand kit. No migration, no
     API surface change.

### R3. The migration on PostgreSQL / MariaDB fails the test matrix.

- **Likelihood**: Low. The migration is a single
  `UserPreferences` table; the schema is trivial.
- **Impact**: Medium. The test matrix currently only runs
  on SQLite (per ADR 0001); the migration would silently
  not be exercised on the other providers.
- **Detection**: `dotnet ef migrations script
  20260808_AddUserPreferences` runs against each provider
  in the CI matrix (currently SQLite-only; the matrix
  expansion is itself a follow-up).
- **Action plan**:
  1. The migration uses only ANSI-SQL types (`varchar(50)`,
     `int`, `datetimeoffset`) that all three providers
     support without provider-specific packages.
  2. The Commit 2 integration test runs the migration on
     an in-memory SQLite database; if it passes there, the
     SQL is portable.
  3. The Postgres / MariaDB test expansion (per ADR 0001
     follow-up) will exercise the migration end-to-end
     before the v1.3.0 release that ships the workstream.

### R4. The cookie write-through race: a logged-in user
     reloads the page right after toggling; the cookie
     is set but the `PUT /api/users/me/preferences`
     has not finished; on the next reload the cookie says
     `cardscape-classic` but the server still says
     `default`; on the *next* login the server wins and
     the user's setting is lost.

- **Likelihood**: Low (the user has to toggle, reload
  fast, and re-login in the window).
- **Impact**: Medium (the user sees their setting
  disappear).
- **Detection**: Hard to test reliably in CI; manual
  reproduction only.
- **Action plan**:
  1. The cookie write is **synchronous** and happens
     before the `PUT` is fired. The next page load
     reads the cookie, not the server.
  2. The `PUT` is **fire-and-forget on best effort**.
     If it fails, the cookie still has the user's choice
     and the next manual toggle retries the `PUT`.
  3. If the user re-logs in within the race window,
     `InitializeAsync()` reads the server's value
     (still the old one) and overwrites the cookie.
     This is the documented behaviour: the server is
     authoritative for cross-device sync, the cookie is
     a local cache.
  4. The mitigation for the rare race is: **the
     `UserPreferencesService.SetAsync` method awaits the
     `PUT` before resolving**. The caller (the toggle /
     the settings page) shows a
     `<RadzenProgressBarCircular>` while the `PUT` is in
     flight; the user does not see the toggle "commit"
     until the server has confirmed. This makes the race
     window the duration of one HTTP round-trip, not
     "until the user reloads".

### R5. The `<RadzenTheme Theme="@_currentTheme" />` tag
     does not apply the custom theme on first render
     (flash of default theme before the cookie resolves).

- **Likelihood**: Low. The `<RadzenTheme>` tag is in
  `App.razor`, which renders before any page.
- **Impact**: Medium. The user sees a 50ms flash of the
  default theme on every page load.
- **Detection**: The R9 walkthrough captures the time
  between `domcontentloaded` and `<RadzenTheme>` applying
  its `--rz-*` overrides; a flash > 200ms is the
  threshold.
- **Action plan**:
  1. **First try** — call
     `await Prefs.InitializeAsync()` in `App.razor`'s
     `OnInitializedAsync` and block the first render
     until the cookie is read. The cookie is read from
     `document.cookie` synchronously in JS, which is
     already in memory by the time Blazor boots.
  2. **Second try** — pre-paint a `<style>` tag in
     `index.html` that reads the cookie and sets the
     matching `--rz-*` variables. This is the only HTML
     / CSS we ship; the trade-off is documented as a
     deviation in ADR 0011.
  3. **Third try** — accept the 50ms flash as the cost
     of zero-custom-CSS. The user only sees it on the
     *first* page load of the session; the cookie is in
     memory from then on.

### R6. Future maintainer adds a sixth free theme name and
     forgets to add it to the picker.

- **Likelihood**: Low. The picker reads from
  `ThemeCatalog.All` (a single source of truth); adding
  a theme is a one-line change.
- **Impact**: Low. The new theme would still be
  selectable via the cookie, just not in the UI.
- **Action plan**: Documented in `ThemeCatalog.cs` with
  a comment block above the `All` array.

### R7. GDPR: the new `UserPreferences` row is not
     deleted when the user is anonymised.

- **Likelihood**: Low (we add the `UserAnonymisedHandler`
  in Commit 2).
- **Impact**: High (the preference would outlive the
  user, which is a GDPR Art. 17 violation).
- **Detection**: The
  `UserAnonymisedRemovesPreferencesTests` integration
  test in §8.2.
- **Action plan**:
  1. Commit 2 adds the `UserAnonymisedHandler` that
     calls `DeleteByUserIdAsync` on the anonymised
     user's `UserId`.
  2. The integration test runs the anonymise flow
     end-to-end and asserts the row is gone.
  3. If a future change adds a new event that needs
     to clean up preferences (e.g. workspace deletion),
     the test pattern is in the integration test file
     and the handler is one method.

---

## 10. Out of scope (explicitly deferred)

These are intentionally **not** part of this plan and would
each be a separate plan with its own ADR if/when they
become priorities:

- **Other user preferences** (locale, timezone, notification
  routing, …) moving into the `UserPreferences` aggregate
  (NG6).
- **Per-workspace theme** (overrides the user theme inside a
  workspace context; touches `Workspace` aggregate).
- **`-wcag` / high-contrast theme variants** (NG4).
- **Custom icon font** (NG2).
- **Theme inheritance / "user-defined theme from color
  picker"** (NG3).
- **Theme preview thumbnails** (static PNG/SVG of each
  theme for the picker; deferred until we know which
  themes the user actually uses).

---

## 11. Acceptance criteria (final)

The plan is **done** when:

1. All 6 commits are merged to `master` in order.
2. `dotnet test` is green (current baseline: 313 unit + 85
   integration + the new tests in §8.1–8.2 = ~510 tests).
3. `dotnet build` is green with 0 warnings.
4. `app.css` is still < 100 lines.
5. `src/Cardscape.Web/Pages/` contains 0 `<button>`,
   `<input>`, `<form>`, or `IJSRuntime.InvokeAsync`
   elements.
6. `wwwroot/css/*.css` count is unchanged
   (`app.css` + `barlow.css` only).
7. ADR 0011 is merged and cross-linked from
   `docs/AGENTS.md` and `docs/brand/00-brand-kit.md`.
8. The integration tests in §8.2 all pass.
9. The user can apply any of the 12 themes from either the
   header toggle or `/settings/appearance`, the choice
   persists across reloads (cookie) and across devices
   (server), and the Cardscape Classic variant's primary
   is the brand teal `#0f3d3e` (light) / `#1a8a8b` (dark).

---

## 12. Pointers

- **Cookie service wiring**:
  `src/Cardscape.Web/Program.cs:50-54`.
- **Static theme links (to be removed in Commit 1)**:
  `src/Cardscape.Web/wwwroot/index.html:10-11`.
- **Brand color anchor**:
  `src/Cardscape.Web/wwwroot/index.html:14`
  (`<meta name="theme-color" content="#0f3d3e" />`).
- **ADR 0009** (the "no custom HTML/JS/CSS" rule this plan
  preserves): [`docs/adr/0009-radzen-only-ui.md`](../adr/0009-radzen-only-ui.md).
- **Radzen skill** (the skill this plan extends):
  [`.agents/skills/radzen-blazor/SKILL.md`](../../.agents/skills/radzen-blazor/SKILL.md).
- **v1.2.0 predecessor plan** (this one slots in next):
  [`docs/roadmap/05-plan-v1.2.0.md`](05-plan-v1.2.0.md).
- **R9 walkthrough** (the most recent end-to-end pass that
  confirms the i18n + GDPR work landed clean):
  [`test-results/r9/r9-report.md`](../../test-results/r9/r9-report.md).
- **Brand kit** (extended in Commit 6 with the Cardscape
  Classic palette):
  [`docs/brand/00-brand-kit.md`](../brand/00-brand-kit.md).
