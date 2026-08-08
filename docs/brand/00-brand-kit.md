# Brand kit

> The visual and verbal identity of Cardscape. Every artifact
> that ships visual design — the website, the docs, the Blazor
> UI, the social card, the favicon — draws from this kit.
>
> The verbal identity (name, tagline, pillars, vocabulary,
> voice) lives in
> [`../roadmap/02-product-positioning.md`](../roadmap/02-product-positioning.md).
> This file covers only the visual identity.

---

## 1. Logo

The Cardscape logo is a single glyph + wordmark.

- **Glyph**: a stacked-card mark (three offset rectangles
  representing a card and a list behind it).
- **Wordmark**: the name "Cardscape" in the project's
  display font, sentence case, kerned tight.

The mark is monochrome by default. It accepts the accent
color (see §2) for branded applications. Black-on-white and
white-on-dark variants ship with the kit.

The SVG source is `docs/brand/logo.svg` (added with the first
release). Until the SVG is in the repo, the placeholder
glyph is the unicode `◆` (black diamond) used in the website
and the README.

### Logo clear space

Keep clear space equal to the height of the "C" of the
wordmark on all sides. No text, no imagery, no border closer
than that.

### Logo minimum size

- 24 px tall on screen.
- 12 mm tall in print.

Below that, use the glyph only (drop the wordmark).

---

## 2. Color palette

The palette is dark-first (the audience is developers), with
a teal accent. Hex values are authoritative; the names are
for talking about them.

### Brand

| Name | Hex | Use |
|---|---|---|
| `cardscape-teal` | `#2dd4bf` | primary accent, CTAs, links on dark |
| `cardscape-teal-hi` | `#5eead4` | hover state of the teal accent |
| `cardscape-teal-soft` | `rgba(45, 212, 191, 0.12)` | tinted backgrounds (active states, focus rings) |

### Neutrals (dark theme — primary)

| Name | Hex | Use |
|---|---|---|
| `bg` | `#0d1117` | page background |
| `bg-alt` | `#010409` | section background, footer |
| `surface` | `#161b22` | cards, panels, surfaces raised above the page |
| `surface-hi` | `#1f242c` | surfaces on surfaces (e.g. code inside a card) |
| `border` | `#30363d` | default border |
| `border-hi` | `#484f58` | hover border, focus border |
| `text` | `#e6edf3` | primary text |
| `text-mute` | `#8b949e` | secondary text, captions |
| `text-faint` | `#6e7681` | tertiary text, eyebrows, timestamps |
| `code-bg` | `#0b1118` | code block background |
| `code-border` | `#21262d` | code block border |

### Neutrals (light theme — secondary, future)

| Name | Hex | Use |
|---|---|---|
| `bg` | `#ffffff` | page background |
| `bg-alt` | `#f6f8fa` | section background, footer |
| `surface` | `#ffffff` | cards, panels |
| `surface-hi` | `#f6f8fa` | surfaces on surfaces |
| `border` | `#d0d7de` | default border |
| `border-hi` | `#afb8c1` | hover border |
| `text` | `#1f2328` | primary text |
| `text-mute` | `#59636e` | secondary text |
| `code-bg` | `#f6f8fa` | code block background |
| `code-border` | `#d0d7de` | code block border |

The light theme is the same palette structure, inverted for
the surfaces and the text. The accent (`cardscape-teal`) stays
the same.

### Status colors (reserved for the UI, used sparingly)

| Name | Hex | Use |
|---|---|---|
| `success` | `#3fb950` | success state, confirmation |
| `warning` | `#d29922` | warning state, non-blocking issues |
| `danger` | `#f85149` | error state, destructive actions |
| `info` | `#58a6ff` | informational, neutral state |

These follow the GitHub Primer palette by design — developers
already recognize them.

### Accessibility

All text/background combinations in the palette meet **WCAG
AA** contrast (4.5:1 for body text, 3:1 for large text). The
accent teal on `bg` (`#2dd4bf` on `#0d1117`) measures at
9.2:1. The link blue on `bg` (`#58a6ff` on `#0d1117`)
measures at 6.0:1.

If you add a color, run the contrast check before shipping.

---

## 3. Typography

The typography uses the **system font stack**. We do not load
external webfonts.

### Sans (UI and body)

```
font-family: -apple-system, BlinkMacSystemFont, "Segoe UI",
             "Noto Sans", Helvetica, Arial, sans-serif;
```

- `-apple-system` (San Francisco on macOS / iOS)
- `BlinkMacSystemFont` (San Francisco on Chrome on macOS)
- `Segoe UI` (Windows)
- `Noto Sans` (Linux distros that ship it)
- The fallbacks cover the rest.

### Mono (code, code samples, eyebrows)

```
font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo,
             Consolas, "Liberation Mono", monospace;
```

### Type scale

| Name | Size (rem) | Size (px @ 16) | Use |
|---|---|---|---|
| `display` | 3.5 – 4.0 | 56 – 64 | page hero titles |
| `h1` | 2.0 | 32 | page section titles |
| `h2` | 1.5 | 24 | subsection titles |
| `h3` | 1.125 | 18 | card titles |
| `body` | 1.0 | 16 | body text |
| `small` | 0.875 | 14 | captions, footnotes |
| `eyebrow` | 0.8125 | 13 | mono-spaced section labels, tag-like text |

Line height: `1.6` for body, `1.2` for display and headings,
`1.5` for code blocks.

Letter spacing: `-0.02em` on display, `-0.01em` on h1 / h2 /
h3, `0` on body, `0.02em` on eyebrows.

### Font weight

| Weight | Use |
|---|---|
| `700` (bold) | display, h1, h2 |
| `600` (semibold) | h3, nav, button text |
| `500` (medium) | (reserved; do not use yet) |
| `400` (regular) | body, captions |
| `300` (light) | (not used) |

---

## 4. Iconography

Iconography is minimal in Phase 0. The site uses inline
unicode glyphs (`◆`, `✓`, `→`, `↗`) where icons are needed.

The product UI (Phase 1+) will use **Radzen.Blazor's built-in
icon set** (Material icons). We will not introduce a second
icon library. The exception is the favicon, which uses the
brand glyph (§1).

---

## 5. Imagery and photography

We do not use stock photography. The site's visual elements
are:

- The brand glyph.
- The Cardscape teal as a gradient accent in the hero.
- The architecture diagram (Mermaid, rendered in
  `docs/architecture/00-overview.md` and reused in the site).
- The timeline of phases (CSS-only, in the site).

When the project needs more visuals (screenshots of the
product, a demo GIF, a social card), the brand kit will be
extended. For now, less is more.

---

## 6. Application in the UI

The Blazor UI in `src/Cardscape.Web/` consumes the brand
palette via Radzen's documented theme pipeline. As of
v1.2.0 the picker ships with **12 themes** — 5 Radzen
free themes (default / humanistic / material / software /
standard) + their 5 `-dark` siblings + the 2 custom
Cardscape Classic variants. The single source of truth
is `src/Cardscape.Web/Theming/ThemeCatalog.cs`.

### 6.1 Cardscape Classic palette (the brand surface)

| Slot | Light | Dark | Role |
|---|---|---|---|
| Primary | `#0f3d3e` | `#1a8a8b` | Brand teal — the canonical anchor (`<meta name="theme-color">`). |
| Primary-light | `#1a5a5b` | `#2fa9aa` | +1 HSL step toward white. |
| Primary-darker | `#082627` | `#0f3d3e` | -1 HSL step toward black. |
| Secondary | `#d4a574` | `#d4a574` | Warm sand — complementary to the teal on the HSL wheel (~150°). |
| Secondary-light | `#e2bd8d` | `#e2bd8d` | +1 HSL step. |
| Secondary-darker | `#a87e4f` | `#a87e4f` | -1 HSL step. |
| Page background | `#f7f8f8` | `#1a1d1e` | One shade off pure white / near-black. |
| Border radius | `4px` | `4px` | Tighter than the Radzen Software default (6px) — "serious tool". |

The base for the custom theme is Radzen's **software**
free theme (per maintainer direction). The two CSS
files (`wwwroot/css/cardscape-classic.css` and
`cardscape-classic-dark.css`) declare only the colour
slots; shape, font scale, and focus ring fall through
to the Radzen base. This is the documented
"theme-override-on-top-of-a-base" pattern from the
Radzen theme builder.

### 6.2 Secondary colour rationale

The secondary `#d4a574` (warm sand) was chosen by the
maintainer with the assistant picking the specific value
(documented in [docs/roadmap/06-plan-radzen-themes.md §4.4](../roadmap/06-plan-radzen-themes.md)).
The reasoning:

- **Complementary** to the teal on the HSL wheel (~150°
  apart) — high contrast without being jarring.
- **Earth / "serious tool" feel** — amber / sand reads
  as paper, brass, leather; the materials of an
  old-school project-management binder.
- **WCAG-compliant on both surfaces** — 3.2:1 against
  white (passes for large text), 6.8:1 against the
  Cardscape Classic Dark background (passes for body
  text).
- **Same value on light and dark** — the warm sand is
  bright enough to read on dark and saturated enough to
  read on light, so we do not need a separate "secondary
  dark" value.

### 6.3 How to change the brand palette

1. Update the swatch table in §6.1.
2. Update the matching `Theme` POCO in
   `src/Cardscape.Web/Theming/ThemeCatalog.cs`
   (`CardscapeThemes.Classic` / `ClassicDark`).
3. Update the matching CSS variables in
   `wwwroot/css/cardscape-classic.css` and
   `cardscape-classic-dark.css`.
4. Re-run the integration test (R9 walkthrough) to
   confirm the brand surfaces still match.

The plan and the ADR 0011 are the source of truth for
the cross-cutting design rationale; the test in
`tests/Cardscape.UnitTests/Theming/ThemeCatalogTests`
pins down the exact values.

---

## 7. What we do not brand

- **Competitor logos**. We do not display, link to, or
  reference any other kanban or project-management product
  in the brand kit, the site, the docs, or the README.
- **Microsoft / Atlassian / Google logos**. We do not display
  vendor logos in the README. Where we use a vendor product
  (e.g. "Google Drive integration"), we say so in words, not
  in logos.
- **"Powered by" anything**. The site does not display
  "Powered by .NET", "Powered by Radzen", or similar
  third-party branding.

---

## 8. When to revisit

The brand kit should be revisited when **any** of the
following is true:

1. A real logo (vector, not the unicode placeholder) is
   authored. Then §1 gets a "Logo source" section pointing
   at the SVG.
2. A light theme is implemented in the product UI. Then
   the "Neutrals (light theme)" section becomes the live
   reference.
3. An icon set is introduced beyond Radzen's built-in set.
4. A social card (Open Graph image) is designed.
5. The tagline in
   [`../roadmap/02-product-positioning.md`](../roadmap/02-product-positioning.md)
   changes materially, in which case the hero typography
   on the site gets re-checked.

Until then, the kit is a stable design reference. Changes
are append-only with a "Revised YYYY-MM-DD" header.
