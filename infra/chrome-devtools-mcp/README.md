# Chrome DevTools MCP (Docker)

Self-contained image that runs the official `@chrome-devtools-mcp`
Node package against a real (not Chromium) Google Chrome, exposed
over MCP stdio.

## Why Docker

- **No local Chrome dependency.** Useful on a headless box, in a
  CI runner, or in a sandboxed WSL distro.
- **Isolated profile.** The `chrome-devtools-mcp-data` volume
  keeps cookies / localStorage / IndexedDB out of the user's
  real Chrome profile, so a beta-test user (e.g.
  `beta-tester@cardscape.test`) does not collide with the
  user's everyday Gmail / banking sessions.
- **Reproducible.** `chrome-devtools-mcp@1.6.0` is pinned in
  the Dockerfile; a future npm release cannot silently change
  the tool surface.

## Build

```bash
docker build -t chrome-devtools-mcp:latest infra/chrome-devtools-mcp
```

The build pulls Google Chrome stable from
`https://dl.google.com/linux/chrome/deb/`, installs the
`chrome-devtools-mcp@1.6.0` npm package globally, and runs a
sanity check that the binary can find Chrome.

## Run

```bash
docker run -i --rm \
  -v chrome-devtools-mcp-data:/data \
  chrome-devtools-mcp:latest
```

The container is **stdio** — no port mapping, no healthcheck.
Mavis (or any MCP client) pipes JSON-RPC over stdin/stdout.
The `-i` flag keeps stdin open; `--rm` cleans up the container
on exit; the named volume persists the Chrome profile.

## Mavis integration

The `C:\Users\Usuario\.minimax\mcp.json` already lists both
options. To prefer the Docker mount, change the entry's
`command` / `args` from the local binary to:

```json
"chrome-devtools": {
  "command": "docker",
  "args": [
    "run", "-i", "--rm",
    "-v", "chrome-devtools-mcp-data:/data",
    "chrome-devtools-mcp:latest"
  ],
  "description": "Chrome DevTools MCP via Docker — see infra/chrome-devtools-mcp/README.md"
}
```

Restart Mavis to pick up the change. (Mavis hot-reloads the
gateway, not the per-server entries; a full restart is the
documented pattern for mcp.json edits.)

## Common runtime flags

| Flag                   | Effect                                                            |
| ---------------------- | ----------------------------------------------------------------- |
| `--no-headless`        | Show a window. Linux + X11 only; ignored on Windows hosts.         |
| `--browser-url=URL`    | Connect to an already-running Chrome via DevTools WebSocket.      |
| `--user-data-dir=PATH` | Override `/data` (e.g. point at a project-specific profile).      |
| `--executable-path=PATH` | Use a different Chrome binary (e.g. Chrome Beta / Canary).      |
| `--headless=new`        | Force the new headless mode (default in Chrome 132+).              |

Pass them after the image name:

```bash
docker run -i --rm -v chrome-devtools-mcp-data:/data \
  chrome-devtools-mcp:latest --no-headless
```

## Why not just `mcr.microsoft.com/playwright`?

The Playwright image ships **Chromium**, not Chrome. The
`@chrome-devtools-mcp` package specifically requires Chrome (it
inspects the binary's `--version` banner and rejects Chromium).
Pulling a 200MB Chromium image and replacing the binary is
strictly more work than installing Chrome directly on a
bookworm base.

## Verifying after build

```bash
docker run --rm chrome-devtools-mcp:latest google-chrome --version
# Google Chrome 132.0.6834.110

docker run --rm chrome-devtools-mcp:latest chrome-devtools-mcp --help
# (prints usage; the help flag returns 0)
```

The image is small (~900 MB on disk) and starts in < 1 s.
