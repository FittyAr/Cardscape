# syntax=docker/dockerfile:1.7
#
# Chrome DevTools MCP — self-contained image that runs the official
# @chrome-devtools-mcp Node package against a real (not Chromium)
# Google Chrome, exposed over MCP stdio. Used by Mavis via the
# `chrome-devtools-mcp` entry in C:\Users\Usuario\.minimax\mcp.json
# when the user prefers the Docker mount (no local Chrome needed,
# isolated profile dir, no cross-project localStorage leakage).
#
# Build:   docker build -t chrome-devtools-mcp:latest infra/chrome-devtools-mcp
# Run:     docker run -i --rm -v chrome-devtools-mcp-data:/data chrome-devtools-mcp:latest
# Verify:  docker run -i --rm chrome-devtools-mcp:latest --help

# ── Stage 1: toolchain ─────────────────────────────────────────────
# node:20-bookworm is the smallest LTS image that has the system
# libraries Chrome's headless build needs (libnss3, libatk-bridge2.0,
# libxkbcommon, libdrm, libgbm, libasound2, libxcomposite, libxdamage,
# libxfixes, libxrandr, libpangocairo, fonts-liberation). Adding
# them by hand on a slimmer base is error-prone; the official
# node-bookworm already pulls them transitively for the Playwright
# Chromium build, so we just layer Chrome on top.
FROM node:20-bookworm AS build

# Install real (not Chromium) Google Chrome stable. We need the
# real one because the @chrome-devtools-mcp package hard-codes the
# Chrome DevTools Protocol surface and the v8 internals;
# Chromium is API-compatible for the page surface but
# chrome-devtools-mcp specifically requires the Chrome binary
# (the package checks the executable's --version output and
# rejects anything that does not start with "Google Chrome").
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        fonts-liberation \
        libasound2 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libc6 \
        libcairo2 \
        libcups2 \
        libdbus-1-3 \
        libexpat1 \
        libfontconfig1 \
        libgbm1 \
        libgcc-s1 \
        libglib2.0-0 \
        libgtk-3-0 \
        libnspr4 \
        libnss3 \
        libpango-1.0-0 \
        libpangocairo-1.0-0 \
        libstdc++6 \
        libx11-6 \
        libx11-xcb1 \
        libxcb1 \
        libxcomposite1 \
        libxcursor1 \
        libxdamage1 \
        libxext6 \
        libxfixes3 \
        libxi6 \
        libxrandr2 \
        libxrender1 \
        libxss1 \
        libxtst6 \
    && curl -sSL https://dl.google.com/linux/linux_signing_key.pub \
        | gpg --dearmor -o /usr/share/keyrings/google-chrome.gpg \
    && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-chrome.gpg] https://dl.google.com/linux/chrome/deb/ stable main" \
        > /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

# Install the MCP server. We pin the version so a future npm
# release cannot silently break the build. The package's
# package.json declares node >= 18; we have 20.
RUN npm install -g chrome-devtools-mcp@1.6.0

# Sanity check that the binary can find Chrome. The package's
# auto-detection walks the standard Linux install paths
# (/usr/bin/google-chrome-stable, /opt/google/chrome/chrome, …)
# and our apt install places the binary at
# /opt/google/chrome/google-chrome which is on the package's
# recognised list.
RUN google-chrome --version \
    && chrome-devtools-mcp --help > /dev/null

# ── Stage 2: runtime ───────────────────────────────────────────────
# Same image as build (no multi-stage strip needed — the only
# apt packages are runtime libs and chrome itself is already
# stripped of its build deps). We just lock down the entrypoint
# and the data dir the MCP needs to keep cookies / localStorage
# across calls.
FROM build AS runtime

# Persistent Chrome profile. The MCP server stores cookies,
# localStorage, IndexedDB, etc. here. Mount a named volume at
# /data to keep the profile across container restarts and to
# share it across projects (so a session that logged into a
# service stays logged in the next time the agent reconnects).
# Without the volume, every container start is a fresh profile
# and the user has to re-login to any non-anonymous resource.
ENV CHROME_USER_DATA_DIR=/data
RUN mkdir -p /data && chmod 1777 /data

# Stdio is the MCP transport. The agent runs
#   docker run -i --rm -v chrome-devtools-mcp-data:/data chrome-devtools-mcp:latest
# and the agent's MCP client pipes JSON-RPC over stdin/stdout.
# No port, no HTTP — this is the standard pattern for stdio MCP
# servers and it works with both Mavis and the Docker MCP
# Gateway's stdio bridge.
ENTRYPOINT ["chrome-devtools-mcp"]
CMD []

# Default flags the operator can override by appending to
# `docker run`. --user-data-dir=${CHROME_USER_DATA_DIR} is the
# MCP server's own flag (not a chrome flag); the rest is the
# chrome-devtools-mcp defaults we want to preserve. We don't
# bake them into ENTRYPOINT because the whole point of the
# Docker mount is that the user can run it the way they want
# (headless vs no-headless, with proxy, etc.). Documented in
# the README; common flags:
#   --no-headless           # use the host's display (Linux only)
#   --browser-url=http://...  # connect to an existing Chrome
#   --user-data-dir=...     # override the volume path
#   --executable-path=...   # use a different Chrome binary
