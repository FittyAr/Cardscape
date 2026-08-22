---
name: Bug report
about: Report something that is broken, behaves wrong, or crashes
title: "[bug] "
labels: ["type:bug", "status:needs-triage"]
assignees: []
---

## What happened

A clear, one-paragraph description of the bug.

## Steps to reproduce

1. `git clone ...` (or "I have an existing install at commit X")
2. `dotnet build`
3. `dotnet run --project src/Cardscape.Api`
4. Navigate to `...`
5. Click on `...`
6. See the error.

## What I expected

The correct behavior.

## What actually happened

The actual behavior, including any error message, log line, or
screenshot. **Paste the full error text** — paraphrasing loses
information.

## Environment

| | |
|---|---|
| Cardscape version | commit SHA / tag / branch |
| .NET SDK | `dotnet --version` |
| OS | (Windows / macOS / Linux + version) |
| SQLite version | |
| Browser (if web) | (name + version) |
| MCP client (if MCP) | (Claude Desktop / Cursor / ...) |

## Reproduction rate

- [ ] Always
- [ ] Intermittent (about 1 in N)
- [ ] Once, can't reproduce again

## Logs

If you have logs, paste the relevant lines here. Use a fenced
code block with the language tag.

```
[your log output here]
```

## Possible cause (optional)

If you have a hunch about what is wrong, share it. It speeds up
the triage even if you are wrong.

## Related

- Issues: # (replace with issue numbers, or "none")
- PRs: # (replace with PR numbers, or "none")
- Docs: link to the doc that says the behavior should be X
