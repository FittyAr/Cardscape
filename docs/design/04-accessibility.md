# Accessibility

> The project's target for **WCAG 2.1 Level AA** compliance
> across the Blazor web client. The MCP server is not
> user-facing; the REST API is not user-facing; the web
> client is. All accessibility work is on the web client.
>
> This is a **design** document. The implementation lands
> across Phase 1 (the initial UI) and Phase 3 (the
> extensions, which add new UI surfaces).

---

## 1. The target

**WCAG 2.1 Level AA.** Not AAA — AA is the right target for
a productivity tool. The AAA bar (e.g. sign language for all
pre-recorded video) is appropriate for some content types
and inappropriate for a kanban UI.

The four POUR principles, applied:

| Principle | What it means in Cardscape |
|---|---|
| **Perceivable** | every UI element is reachable by screen reader, every visual has a non-visual equivalent, color is never the only signal |
| **Operable** | every action is reachable by keyboard, no action requires a precise pointer, focus is always visible |
| **Understandable** | every text is readable, every interaction is predictable, every error is described in plain language |
| **Robust** | the UI works with current and future assistive technology, the markup is valid |

---

## 2. Keyboard navigation

The entire UI is reachable by keyboard. The rules:

- **Tab order is the visual order.** Elements are reached in
  the order a sighted user reads them.
- **Focus is always visible.** The focus ring is a 2px solid
  outline in the accent color, with a 2px offset. The
  focus ring is never `outline: none` (a common anti-pattern).
- **Skip links** are at the top of every page. The first
  link is "Skip to main content" and jumps past the header
  and the navigation.
- **Modal dialogs trap focus** while they are open. The
  focus returns to the trigger element when the dialog
  closes.
- **Drag and drop has a keyboard alternative.** Every
  drag-and-drop interaction (cards, lists) has a "Move"
  menu that achieves the same result with arrow keys.
- **The keyboard shortcuts are documented.** A "?" key
  opens a help dialog listing the shortcuts.

### Default Radzen behavior

Radzen.Blazor components are mostly accessible out of the
box. The exceptions are noted in
[`docs/design/04-accessibility.md`](04-accessibility.md) and
fixed in `src/Cardscape.Web/Accessibility/` overrides:

- `RadzenDialog` — the focus trap is enabled by default;
  verified in our smoke tests.
- `RadzenDropDown` — keyboard navigation works; we add a
  visible label.
- `RadzenDataGrid` — the row selection is announced by
  the screen reader; we add a `aria-label` to the grid.

---

## 3. Screen reader support

The Blazor UI is tested with **NVDA on Firefox** (the
free, open-source combination that most screen-reader users
default to) and **VoiceOver on Safari** (the macOS / iOS
default). The CI runs an **axe-core** accessibility check
on every PR (added in Phase 1).

### Semantic HTML

The UI is built with semantic HTML: `<main>`, `<nav>`,
`<header>`, `<footer>`, `<aside>`, `<article>`, `<section>`,
`<h1>`-`<h6>`, `<ul>` / `<ol>` / `<li>`, `<button>`,
`<label>` + `<input>`, `<table>` with `<thead>` /
`<tbody>` / `<th>`. A `<div>` with a `click` handler is
never acceptable; use a `<button>`.

### ARIA

ARIA is the last resort, not the first. The rules:

- **No ARIA when semantic HTML works.** `<button>` over
  `<div role="button">`.
- **All interactive ARIA controls are keyboard-operable.**
  `role="button"` requires a keyboard handler; `role="tab"`
  requires the arrow-key handler.
- **`aria-label` only when the visible text is insufficient.**
  An icon button needs `aria-label`; a text button does not.
- **`aria-live` regions are reserved for status messages.**
  The "card moved" toast is `aria-live="polite"`; the
  "connection lost" alert is `aria-live="assertive"`.
- **No `aria-hidden="true"` on focusable elements.** It
  confuses the screen reader.

### Forms

Every input has a `<label>`. Every error message is
associated with the input via `aria-describedby`. Required
fields are announced as required. The submit button is
disabled while the form is submitting (announced as
"submitting, please wait").

---

## 4. Color contrast

The color palette in
[`docs/brand/00-brand-kit.md`](../brand/00-brand-kit.md)
meets WCAG AA contrast (4.5:1 for body text, 3:1 for large
text). The verification:

- **Text on background**: every text color in the palette
  is checked against the `bg`, `surface`, and `bg-alt`
  background colors. All combinations pass AA.
- **The accent teal on `bg`** measures at 9.2:1 (well above
  AA's 4.5:1).
- **The link blue on `bg`** measures at 6.0:1 (above AA).
- **The status colors** (`success`, `warning`, `danger`,
  `info`) all pass AA on `bg` and `surface`.

The CI runs a color-contrast check on the rendered CSS
(added in Phase 1). A pull request that introduces a color
combination that fails AA is rejected in review.

### Color is not the only signal

- **Status** is conveyed by color **and** an icon **and**
  text. A red dot is also a ⚠️; the text says "Error".
- **Selection** is conveyed by background color **and** a
  border **and** a checkmark icon.
- **Required fields** are marked with an asterisk **and**
  `aria-required="true"`.

---

## 5. Touch targets

Every interactive element is at least **44×44 CSS pixels**.
The Radzen components are 32×32 by default; we override the
padding to make them 44×44 in the project's theme.

Exceptions:

- A "drag handle" icon inside a card is a 24×24 target on
  the icon, but the entire card is the drag target, and the
  card is at least 88×44.
- A "close" icon on a toast is 32×32. The toast itself
  dismisses on click, so the larger target is the toast
  body.

---

## 6. Motion and animation

Cardscape uses minimal animation. The rules:

- **No animation longer than 200ms.** Anything longer is
  perceived as a delay, not a transition.
- **No animation that loops infinitely.** Spinners loop, but
  the user can disable them in the operating system
  (`prefers-reduced-motion: reduce`).
- **No animation that conveys critical information.** The
  "card moved" toast is a non-animated text update.
- **`prefers-reduced-motion: reduce` is respected.** When
  the user has reduced motion enabled, transitions are
  disabled and replaced with instant updates.

The CSS:

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
```

---

## 7. Internationalization and language

The UI is in English today. When the i18n work lands
(see [`../i18n/01-policy.md`](../i18n/01-policy.md)), the
`<html lang="...">` attribute changes accordingly.

The right-to-left (RTL) languages (Arabic, Hebrew) are
**not** supported today. The layout uses logical CSS
properties (`margin-inline-start` instead of
`margin-left`) where it matters, so the RTL support is
additive in a future phase. The Radzen theme is LTR-only
today.

---

## 8. Accessibility testing

### Automated

- **axe-core** runs in the CI on every PR. The build fails
  if a `serious` or `critical` violation is introduced.
  The tool is wired via `Playwright` (added in Phase 1) and
  runs against the dev server.
- **HTML validation** (the `html-validate` package) runs on
  the rendered output of every page. Invalid markup (a
  `<p>` inside a `<button>`, etc.) is a build failure.

### Manual

- **NVDA on Firefox** is tested manually before every
  release. The smoke test covers: sign in, navigate to a
  board, read a card, move a card with the keyboard, open
  the card detail, add a comment, sign out.
- **VoiceOver on Safari** is tested on macOS at least once
  per phase. The same smoke test.
- **Keyboard-only** is tested manually before every release.
  The smoke test is the same, but the input is the keyboard
  only (no mouse).

### User testing

When the project has at least 10 active users, the
maintainer asks 2-3 of them to test with their preferred
assistive technology. The findings are added to this
document and to the issue tracker.

---

## 9. The accessibility statement

A future PR (with the public launch) adds an
`/accessibility` page on the site, with:

- The conformance level claimed (AA).
- The known limitations (RTL not supported; some Radzen
  components not fully accessible).
- The contact email for accessibility issues
  (`accessibility@fitty.ar`, same forwarding pattern as
  `security@` and `conduct@`).
- The date of the last accessibility audit.

---

## 10. Anti-patterns (do not do this)

- **`outline: none` on a focusable element** — the user
  loses the focus indicator. Use a custom focus style if
  the default does not fit the design.
- **A `<div>` with a `click` handler** — use a `<button>`.
  The `<div>` is not announced as a button, not in the tab
  order, and not operable with the keyboard.
- **An image without an `alt` attribute** — even a
  decorative image needs `alt=""` so the screen reader
  skips it.
- **A form input without a `<label>`** — use `<label for>`,
  not a `<div>` next to the input.
- **A toast that disappears in 3 seconds** — give the user
  enough time to read it (5 seconds minimum) and provide
  an "Undo" action when relevant.
- **A drag-and-drop interaction with no keyboard
  alternative** — the user with motor impairments cannot
  use the app.
- **Color as the only signal** — always pair color with
  text, icon, or pattern.

---

## 11. When to revisit

This document is revisited when:

1. A new UI surface is added (a new extension, a new view)
   and the existing accessibility patterns need to extend
   to it.
2. A new WCAG version is published (currently 2.2).
3. The Radzen.Blazor library ships a major version that
   changes its accessibility defaults.
4. A user reports an accessibility issue that is not
   covered by the current rules.

Until then, this document is the source of truth for
accessibility in Cardscape.
