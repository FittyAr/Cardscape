# Cardscape website

This is the **public website** for Cardscape. It is a single-page
static site, no build step, no JavaScript framework — just HTML
and a CSS file. You can open `index.html` in a browser, drop the
folder on any static host, or wire it up to a CDN.

## What is here

```
site/
├── index.html        # the single page (hero, why, MCP, status, get started, community, license)
├── styles.css        # the single stylesheet
├── README.md         # this file
├── .nojekyll         # for GitHub Pages — disables Jekyll processing
└── og-image.png      # the Open Graph card (TODO: add at v0.1.0)
```

## Preview locally

Open `index.html` in any browser:

```bash
# Windows
start site/index.html

# macOS
open site/index.html

# Linux
xdg-open site/index.html
```

Or serve it with any static file server:

```bash
# Python
python -m http.server 8000 --directory site
# then open http://localhost:8000

# .NET (if you have it)
dotnet serve --directory site
```

## Deploy

Any static host works. The recommended options:

| Host | How to deploy |
|---|---|
| **GitHub Pages** | push this branch, set Pages source to the `site` branch root |
| **Netlify** | connect the repo, set "Publish directory" to `site` |
| **Cloudflare Pages** | connect the repo, set "Build output directory" to `site` |
| **Vercel** | connect the repo, override output to `site` |
| **A static file server** | copy the contents of `site/` to the web root |

The site has no build step. Whatever is in `site/` is what gets served.

## Custom domain

When the project gets a real domain, add a `CNAME` file in
this folder with the bare domain (or subdomain) as the
single line of content, and configure DNS to point at the
host.

The current canonical domain is **`cardscape.fitty.ar`** —
a subdomain of the user's personal domain `fitty.ar`,
hosted on GitHub Pages with a custom CNAME. The `CNAME`
file in this folder contains exactly one line:
`cardscape.fitty.ar`. The DNS record for
`cardscape.fitty.ar` is a CNAME pointing at
`<owner>.github.io`, and the parent `fitty.ar` zone is
configured with the GitHub Pages IP addresses.

To deploy on a different subdomain (or a different
parent domain), update the `CNAME` file, the DNS records,
and the `og:url` and `og:image` meta tags in `index.html`.

## Editing the site

The site is intentionally a single HTML file. When you want to
change content:

1. Edit `site/index.html`.
2. Edit `site/styles.css` only if you are changing the visual design.
3. Open `index.html` in a browser to preview.
4. Commit on the `site` branch.
5. Push.

The content on the site draws from the project's design docs. The
authoritative source for "how Cardscape presents itself" is
[`docs/roadmap/02-product-positioning.md`](../docs/roadmap/02-product-positioning.md)
on the `master` branch. When the positioning doc changes, the
site must be updated to match.

## Open Graph image

There is a placeholder reference to `og-image.png` in
`index.html`. Before the first public release, design and add a
1200 × 630 px social card. A good first version is the wordmark
on the Cardscape teal background, with the tagline underneath.

## Why no build step

The maintainer runs a .NET project, not a Node project. Adding a
build pipeline (npm, webpack, vite, eleventy) for a five-page
static site is a maintenance burden disproportionate to the
value. A plain HTML + CSS site is a 50-year-old format, served by
every web host on the planet, and will work in 2026 and in 2036.

When the project outgrows a single page (e.g. when we add a
separate docs site, a blog, a changelog feed), the migration
path is to introduce Eleventy or Hugo and convert the page
templates. Until then, the simple version wins.
