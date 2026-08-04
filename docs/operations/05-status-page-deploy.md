# Status page — deploy hook (v1.2.0 follow-up)

> Companion to [`docs/status.md`](../status.md) and
> the `site/index.html` in the orphan `site` branch.
> This file documents the **wire** between the repo
> and the public status page. The status page itself
> is just a Markdown file; the wire is the GitHub
> Pages + GitHub Actions workflow that serves it.

## How it works

The Cardscape hosted-service status page is a single
Markdown file at `docs/status.md`. The `site/index.html`
on the orphan `site` branch embeds a 1:1 copy of the
same component table (the `site/index.html` is the
landing page and uses the inline copy; the `docs/status.md`
is the canonical source).

Every push to `master` that touches `docs/status.md` or
`site/index.html` re-builds the `site` branch via the
GitHub Actions workflow at
`.github/workflows/site.yml` (the file is not yet
present in the v1.1.0 release; the recipe below is
the v1.2.0 follow-up the public status page item
called for in the v1.1.0 plan §6.0).

## Recipe (apply to a fresh repo)

1. **Create the orphan `site` branch** if it does not
   exist:
   ```bash
   git switch --orphan site
   git rm -rf .
   git commit --allow-empty -m "chore: initial empty site branch"
   git push origin site
   git switch master
   ```
2. **In GitHub → Settings → Pages**, set the source to
   `Deploy from a branch` → `site` → `/ (root)`. GitHub
   Pages will serve `https://cardscape.github.io/cardscape/`
   (or the custom domain in the `site/CNAME` file).
3. **Add the deploy workflow** at
   `.github/workflows/site.yml`:
   ```yaml
   name: site

   on:
     push:
       branches: [master, main]
       paths:
         - 'docs/status.md'
         - 'site/**'
     workflow_dispatch:

   jobs:
     deploy:
       name: Publish site
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
           with:
             ref: site
             fetch-depth: 0
         - name: Copy status.md and site/ into the work tree
           run: |
             mkdir -p status
             cp ../docs/status.md status/index.md
             cp -r ../site/* .
         - name: Commit and push
           run: |
             git config user.name  "Cardscape site bot"
             git config user.email "site-bot@cardscape.local"
             git add status docs site
             git commit -m "site: publish from $(date -u +%FT%TZ)" || echo "no changes"
             git push origin site
   ```
   The workflow checks out the `site` branch, copies
   `docs/status.md` → `site/status/index.md` and any
   static asset from `site/`, commits, and pushes. The
   `|| echo "no changes"` swallows the empty-commit
   case so a no-op run (the only file touched was the
   workflow itself) does not fail.
4. **Wire the `site` branch push** as the trigger so
   the page rebuilds on every change. GitHub Pages
   re-deploys within ~30 s of the push.

## What the page covers

The status page is the operational surface of the
hosted service. It mirrors the component table from
`docs/status.md`:

| Component | Status | Description |
|---|---|---|
| Web app | 🟢/🟡/🔴 | Blazor WebAssembly client |
| API | 🟢/🟡/🔴 | REST + MCP endpoints |
| MCP server | 🟢/🟡/🔴 | Model Context Protocol server |
| Real-time hub | 🟢/🟡/🔴 | SignalR hub |
| Authentication | 🟢/🟡/🔴 | Email/password + external providers |
| File storage | 🟢/🟡/🔴 | Attachments + archives |
| Search | 🟢/🟡/🔴 | In-memory full-text search |
| AI features | 🟢/🟡/🔴 | Rule-based + OpenAI-compatible |
| Background jobs | 🟢/🟡/🔴 | Internal job dispatcher |
| Database | 🟢/🟡/🔴 | Primary + read replica |

The maintainer flips a row's emoji on every
operational change. The audit at
[`docs/audits/2026-07-30/07-polish.md`](../audits/2026-07-30/07-polish.md)
§5.5 noted that the page is dormant until the first
self-hosted instance with a public URL wants to wire
it up — this file is the recipe.

## Local preview

```bash
# Clone the site branch locally and serve it with any
# static-file server. The Python one-liner is enough.
git clone --branch site --depth 1 https://github.com/cardscape/cardscape.git /tmp/cardscape-site
cd /tmp/cardscape-site
python -m http.server 8080
# open http://localhost:8080/status/
```

The `status/index.md` is rendered by the GitHub Pages
Jekyll theme by default; the same file works on any
Markdown-aware static site generator.
