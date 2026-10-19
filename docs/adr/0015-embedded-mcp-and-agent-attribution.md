# ADR 0015 — Embedded MCP server, personal AI agents and agent attribution

- Status: Accepted
- Date: 2026-10-19
- Supersedes (in part): [ADR 0002](0002-mcp-server.md) — the MCP surface is
  unchanged, but it no longer requires a second deployable.

## Context

Cardscape ships an MCP server (`src/Cardscape.Mcp`), but only as a separate
process. The single-container deployments (`docker-compose*.yml`, 1Panel) never
started it, there was no way for an administrator to switch it on or off, and
the Web UI gave users no hint how to connect an agent. API tokens existed but
were all-or-nothing (`read` / `write`), and every change an agent made was
recorded exactly as if the person had made it by hand.

We want:

1. The Cardscape administrator decides whether the MCP server runs.
2. Once it runs, every user mints their own key for their own agent, and
   chooses what that agent may do. An agent can never do more than its owner:
   it acts with the owner's workspace/board roles, narrowed further by the
   key's permissions and by the administrator's ceiling.
3. Everything an agent changes is attributed to "the owner's AI", not to the
   owner, everywhere a change is recorded: board activity, the administration
   audit log, and the server logs.

## Decision

### 1. MCP runs inside the API, behind an admin switch

- `Cardscape.Mcp` becomes a class library (tools, resources, prompts,
  authorization, broadcaster). The old standalone process moves to
  `Cardscape.Mcp.Host`, kept for operators who want to scale MCP separately.
- The API maps the MCP endpoint at **`/mcp`** (stateful Streamable HTTP,
  API-key bearer only). In-process, realtime fan-out needs no HTTP hops: board
  changes reach subscribed MCP sessions directly, and tool mutations reach Web
  clients through the API's existing domain-event broadcasters.
- New settings section `SystemSettings.Agents`:
  - `McpEnabled` (default **off**). While off, `/mcp` answers 404 and the UI
    hides the agent page's connection details.
  - `AllowWrites` (default on). The instance-wide ceiling: when off, every
    agent is read-only regardless of its own permissions.
- `PublicInstanceSettings` exposes `McpEnabled` + `AgentWritesAllowed` so the
  Web UI can show or hide the agent features.

### 2. Personal agent keys with narrowed permissions

The existing `ApiToken` aggregate is the agent key (one row per agent; its name
is the agent's name, e.g. "Claude Desktop"). Scopes become granular:

| Scope | Grants |
| --- | --- |
| `read` | Every read-only tool, resource and prompt. |
| `cards:write` | Cards, checklists, votes, snoozes, recurrence, card labels and field values, inbox. |
| `comments:write` | Add, edit and delete the owner's comments. |
| `boards:write` | Boards, lists, labels, custom-field definitions, automation, dashcards, extensions, imports. |
| `workspace:write` | Invitations, OAuth apps and integrations. |
| `write` | Legacy: every write capability above (still accepted for REST). |

Each MCP write tool is classified into exactly one capability; the existing
closed-catalog test keeps the classification exhaustive. A call is allowed only
when **all** of these hold: the key has the capability (or `write`), the
administrator allows agent writes, and the Application layer's normal
role checks pass for the owner. The key therefore can only narrow what the
owner may do, never widen it.

### 3. Attribution: "Ivan · AI"

Requests authenticated with an agent key carry `token_id` and a new
`agent_name` claim. A persistence interceptor stamps every row written in that
request:

- `Activity.AgentId` / `Activity.AgentName` (board and card timelines).
- `AuditEntry.ActorAgentId` / `AuditEntry.ActorAgentName` (admin audit log).

Server log events get `ActorKind=agent`, `AgentId` and `AgentName`
properties.

The UI shows the actor as **"Ivan · AI"** ("Ivan · IA" in Spanish) with a robot
badge whose tooltip names the agent ("Agent: Claude Desktop"). Exports use the
same label, so a plain-text log still tells "Ivan" and "Ivan's AI" apart.

## Consequences

- One container runs everything; turning MCP on is a checkbox, not a deploy.
- New nullable columns on `activities` and `audit_entries` (all four providers).
- Existing `read`/`write` keys keep working unchanged.
- Fine-grained capabilities are enforced on the MCP surface; on REST, an API key
  still needs `write` for mutations (fine-grained keys are read-only there).
