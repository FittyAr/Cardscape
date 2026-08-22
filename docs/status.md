# Cardscape status

> Public status page for the Cardscape hosted service.
> Served from the `site` branch via GitHub Pages.

This page lists the current operational state of every
user-facing component of the Cardscape hosted service.
**Self-hosted deployments** are not covered here — operators
of self-hosted instances should run their own monitoring and
point users at their own status URL.

## Components

| Component | Status | Description |
|---|---|---|
| Web app | 🟢 Operational | Blazor WebAssembly client served from `app.cardscape.example` |
| API | 🟢 Operational | REST + MCP endpoints served from `api.cardscape.example` |
| MCP server | 🟢 Operational | Model Context Protocol server on the same host as the API |
| Real-time hub | 🟢 Operational | SignalR hub backing live board / card updates |
| Authentication | 🟢 Operational | Email/password, Google, Microsoft, Apple, SAML, SCIM |
| File storage | 🟢 Operational | Attachments + import/export archives |
| Search | 🟢 Operational | Relational search across current rows in boards the user can read |
| AI features | 🟢 Operational | OpenAI-compatible provider; local Ollama endpoint by default |
| Background jobs | 🟢 Operational | Internal job dispatcher (no Hangfire) |
| Database | 🟢 Operational | SQLite persistent store |

## Last incident

> _No incidents in the last 90 days._

A historical log of resolved incidents is published at
[`docs/operations/05-incident-log.md`](operations/05-incident-log.md).

## Reporting a new incident

- **Customers** — open a support ticket at
  <https://cardscape.example/support> or email
  <support@cardscape.example>.
- **Self-hosted operators** — follow your own incident
  response procedure; this page only covers the hosted
  service.

The incident response procedure for the hosted service is
documented at
[`docs/operations/04-incident-response.md`](operations/04-incident-response.md).

## Subscribe

A public RSS feed of status changes lives at
`/status.rss` and is updated by the same workflow that
posts the commit to this page.
