# 0009 — Radzen-only UI: kill HTML/JS/CSS custom in Cardscape.Web

> **Status**: Accepted
> **Date**: 2026-08-03
> **Supersedes**: (none)
> **Related**: [docs/AGENTS.md §8](../AGENTS.md#8-available-agent-skills-project-local), [docs/refactoring/01-audit.md](../refactoring/01-audit.md), [docs/refactoring/02-plan.md](../refactoring/02-plan.md)

## 1. Context

Cardscape.Web (Blazor WebAssembly) is a kanban-and-everything-else
client. The original port from Razor Pages carried over 1,517 lines
of custom `app.css` (auth-, home-, landing-, board-, card-,
calendar-, planner-, rule-, inbox-, token-, invitation-, member-,
extension-, etc. classes), 2 instances of `IJSRuntime.InvokeAsync`
(one a literal `eval()` XSS vector), 2 raw `<button>` elements
inside the calendar/planner views, and ~3 MB of unused Bootstrap
5 assets in `wwwroot/lib/bootstrap/`.

The contract documented in [`docs/AGENTS.md`](../AGENTS.md#8-available-agent-skills-project-local)
already declares `radzen-blazor` as the project-local skill for
any UI change, but the code itself had drifted from that contract.

## 2. Decision

**The Cardscape.Web client uses Radzen.Blazor exclusively.**

The only exceptions are:

1. **CSS isolation of Blazor** (`.razor.css` files) for the three
   shared components that Radzen does not cover end-to-end:
   - `Shared/KanbanBoard.razor` + `.razor.css` — Kanban-style
     horizontal scroll with columns of cards. `RadzenDataGrid`
     sacrifices the column metaphor; `RadzenScheduler` is a
     time-grid and renders the cards as scheduled events (wrong
     shape for this UI). We re-implement the kanban with the
     existing `RadzenCard` for each card and CSS isolation for
     the column/row layout, scoped to that component only.
   - `Shared/MonthCalendar.razor` + `.razor.css` and
     `Shared/MonthPlanner.razor` + `.razor.css` are replaced
     by `RadzenScheduler` (which is part of `Radzen.Blazor` and
     ships free of charge, unlike some other Radzen components).
     The scheduler's `RadzenMonthView` covers the calendar
     case; the planner is built on `RadzenScheduler` with the
     `RadzenWeekView` + per-swimlane group template.
2. **Standard Blazor WASM template elements** in `app.css`:
   the loading spinner (`.loading-progress`, `.loading-progress-text`)
   and the error overlay (`#blazor-error-ui`, `.blazor-error-boundary`).
   These are required by the Blazor WebAssembly SDK and Radzen
   does not replace them.
3. **Accessibility override** for `prefers-reduced-motion: reduce`
   that kills Radzen's CSS transitions globally. Lives in
   `app.css` because the override must apply to every Radzen
   element regardless of which component renders it.

The `app.css` after the migration is **< 100 lines** (down
from 1,517) and contains only the items above. The full visual
layer comes from Radzen components, the Radzen cookie theme
service (already wired in `Program.cs`), and per-component
`.razor.css` files for the three components above.

## 3. Rationale

### Why not keep the custom CSS in place?

- **Maintenance**: a 1,500-line `app.css` with classes for
  `auth-shell`, `auth-card`, `auth-field`, `inbox-item--unread`,
  `planner-card`, etc. is debt that compounds. Every new page
  reaches into the same file and the surface keeps growing.
- **Accessibility**: Radzen components ship with WCAG-compliant
  keyboard navigation, focus management, and ARIA semantics.
  The custom `<button class="calendar-entry">` had none of that.
- **Theming**: the Radzen theme service is cookie-backed
  (`AddRadzenCookieThemeService`) and gives every user a
  light/dark/humanistic/material toggle for free. The custom
  CSS hard-coded colors and would not respond to the toggle.
- **Agreements**: `docs/AGENTS.md` §8 already lists
  `radzen-blazor` as the required skill for UI work. The audit
  ([`docs/refactoring/01-audit.md`](../refactoring/01-audit.md))
  showed the code did not match the contract.

### Why CSS isolation for the kanban, and not `RadzenScheduler` or `RadzenDataGrid`?

- `RadzenScheduler` is time-grid by design (days × hours, with
  optional `RadzenMonthView` that paints a *month*). The Kanban
  card metaphor (lanes, cards with `position: relative` inside
  a `position: absolute` parent, vertical scroll) does not map
  to either. A scheduler view would have to fake lanes via
  resources and it would feel wrong to users coming from
  Kanban.
- `RadzenDataGrid` is row-based; forcing it into a column view
  loses the card-look, the drag affordance, and the ability
  to peek at a card without opening it.
- The custom `kanban` CSS was already well-encapsulated. Moving
  it into a `Shared/KanbanBoard.razor.css` keeps the rule names
  prefixed with `.cs-kanban-` so they cannot collide with
  anything else, and the markup moves into a generic
  `<KanbanBoard TItem="...">` component that `BoardDetail.razor`
  consumes with render fragments.

### Why `RadzenScheduler` for Calendar and Planner (instead of custom grids)?

- `RadzenScheduler` (free, part of `Radzen.Blazor`) covers day,
  week, and month views with built-in event rendering, date
  navigation, and the same Radzen theming. The original custom
  `MonthCalendar` grid was a 70-line `app.css` block plus 50
  lines of markup that re-implemented month math (`daysInMonth`,
  `leadingBlanks`, Monday-first offsets, …) — all of that
  becomes one `<RadzenScheduler>` with `<RadzenMonthView>`.

### Why self-host Barlow (instead of keeping Google Fonts CDN)?

- The original `index.html` had a hard dependency on
  `fonts.googleapis.com`, which leaks the user's IP to Google
  on every page load.
- The CSS already named `Sora` and `Fraunces` as the display
  and body fonts, but only Barlow was actually loaded — a
  bug waiting to happen if the Google Fonts request failed.
- Self-hosting four weights of the Barlow latin subset is
  ~90 KB on disk and 0 KB of external requests.

## 4. Consequences

### Positive

- `app.css` shrinks from 1,517 to < 100 lines.
- 0 `IJSRuntime.InvokeAsync` in the Web project (was 2,
  including a literal `eval()` XSS vector in
  `OAuthCallback.razor:48`).
- 0 raw `<button>`, `<input>`, or `<form>` inside the .razor
  files (was 2, 0, and 0; the 2 buttons are now
  `RadzenButton`).
- 0 unused Bootstrap assets under `wwwroot/lib/` (was ~3 MB
  of orphan CSS + JS).
- Every new page can be built from `PageHeader`,
  `LabeledField`, `MetadataList`, `RadzenDataGrid`,
  `RadzenScheduler`, `RadzenCard`, `RadzenStack`, and
  `RadzenAlert` — no per-page CSS unless the page is one of
  the three components above.
- Every Radzen element honors `prefers-reduced-motion: reduce`.

### Negative / trade-offs

- The kanban CSS (~60 lines) lives in
  `Shared/KanbanBoard.razor.css` instead of the global file.
  This is by design (CSS isolation) but means a developer
  who only reads `app.css` will not find the kanban styles.
  The component is small enough that the coupling is obvious.
- Switching the Radzen theme (e.g. from `default` to
  `humanistic`) does not cascade into the custom
  `.cs-kanban-*` classes. They use hard-coded greys
  (`#ebecf0`, `#f4f5f7`, …) and the kanban will keep the
  same look across themes. Acceptable: the kanban is meant
  to look like a Kanban board, and the Kanban color
  palette is iconic.

## 5. Compliance

This decision is enforced by:

1. The acceptance checklist at the top of
   [`docs/refactoring/02-plan.md`](../refactoring/02-plan.md).
2. The build will not break if the audit regresses (the
   classes still exist as no-ops) but a follow-up CI step
   could grep for the forbidden class names.
3. `docs/AGENTS.md` §8 already lists `radzen-blazor` as the
   required skill for UI work; this ADR formalizes the
   "no HTML/JS/CSS custom" rule that the skill already
   implies.
