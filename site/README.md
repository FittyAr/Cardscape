# Cardscape website

Static site for <https://cardscape.fitty.ar>: HTML, CSS and vanilla JS, no build step.
Bilingual (English and Spanish) with a language switcher; the choice is stored in `localStorage`.

```
site/
├── index.html          landing page
├── changelog.html      version history (reads data/changelog.json)
├── data/               generated at publish time, git-ignored (see "Version and changelog")
├── css/                base.css, index.css, changelog.css
├── js/                 translations.js (i18n), common.js, index.js, changelog.js
├── assets/             icon.svg, logo.svg, social.png, screens/ (WebP screenshots, en-* and es-*)
├── llms.txt, robots.txt, sitemap.xml
├── tools/showcase/     how the screenshots are produced
├── CNAME               cardscape.fitty.ar (kept on publish)
└── .nojekyll
```

## Preview

```bash
python -m http.server 8000 --directory site   # http://localhost:8000
```

## Version and changelog

The version badge and the changelog are derived from git, not from GitHub Releases (the project
publishes tags only). `scripts/site-data.py` reads every `vX.Y.Z[-codename]` tag, takes the notes from
the matching `## [<tag>]` section of `docs/community/CHANGELOG.md` (or the tag message), and writes:

- `data/version.json`: newest tag, normalised to `vX.Y.Z` (the codename suffix is shown separately).
- `data/changelog.json`: every tag, newest first.

Nothing is hard-coded in the pages. Without `data/` (plain local preview) they fall back to the GitHub
tags API. Generate the files locally with:

```bash
python scripts/site-data.py           # warns if Directory.Build.props <Version> != newest tag
python scripts/site-data.py --check   # same, but fails (used by the CI release job on tags)
```

## Publishing

`.github/workflows/site.yml` runs on every push to `master` that touches `site/**`, the changelog,
`Directory.Build.props` or the generator, and on every pushed `v*` tag. It runs `scripts/site-data.py`, rsyncs this
folder into the `site` branch (keeping that branch's `CNAME`) and pushes it. GitHub Pages serves the
`site` branch root.

## Editing

All user-visible text lives in `js/translations.js` (`es` and `en`). Elements use `data-i18n`
(and `data-i18n-alt` for image alt text). Add a key to both languages when you add text.
Screenshots are chosen per language (`assets/screens/<lang>-<name>.webp`).

## Screenshots

Real captures of Cardscape running locally on the built-in fictional demo company ("Nexora Studios",
`*.example` addresses). See `tools/showcase/README.md`. Never publish captures of real data.
