# Feature inventory

> The target feature surface for Cardscape as an open-source kanban
> and project-management tool. This document is **not** a feature
> comparison; it is the set of features we are intentionally building
> toward, informed by the kanban-tool landscape in 2026 and by the
> maintainer's direction (see `01-implementation-plan.md`).

The implementation plan that turns this inventory into a delivery
schedule is in [`01-implementation-plan.md`](01-implementation-plan.md).

---

## 1. Core model

| Concept | Description |
|---|---|
| **Workspaces** | Top-level container that groups boards and members |
| **Boards** | The kanban surface. Holds lists, members, labels, extensions. Has a name, description, background, visibility. |
| **Lists** | Columns within a board. Ordered, can be archived. Hold cards. |
| **Cards** | The atomic unit. Title, description, members, labels, due date, attachments, comments, checklists, custom fields, cover image, activity. |
| **Members / Users** | Profile (name, avatar, email, initials). Assigned to cards, boards, workspaces. |

## 2. Card-level features

| Feature | Description |
|---|---|
| **Title** | Required, ≤ 512 chars. |
| **Description** | Long-form text with rich-text editor (mentions, attachments, formatting). |
| **Members / assignees** | One or more users assigned to the card. |
| **Due date** | Optional date with optional time. Reminders configurable. |
| **Labels** | Color-coded tags per board. |
| **Checklists** | Subtasks on the card. Each item has a name, optional due date, optional assignee, completed state. |
| **Attachments** | Files (size cap is a deployment concern, not a product limit). Links with previews. |
| **Comments** | Conversation on the card, with mentions, reactions, edits. |
| **Activity** | Append-only log of every change to the card. |
| **Cover** | Image or color used as the visual background. |
| **Custom fields** | Dropdowns, dates, numbers, text — schema defined per board. |
| **Card mirror** | Same card linked across multiple boards. |
| **Card aging** | Visually fades cards that haven't seen activity. |
| **Card snooze** | Hide the card until a date/time. |
| **Card repeater** | Create a copy of the card on a schedule. |
| **Voting** | Members add votes to cards. |
| **Watch** | Subscribe to a card's notifications. |

## 3. Board-level features

| Feature | Description |
|---|---|
| **Drag and drop** | Move cards within and across lists. Reorder lists. |
| **Board templates** | Pre-built boards (Kanban, Scrum, Calendar, etc.). |
| **Board filtering** | Filter cards by member, label, due date, custom field. |
| **Board background** | Color or image (including uploaded custom images). |
| **Archive** | Archive cards and lists. Board-level archive accessible from a sidebar. |
| **Activity log** | All changes to the board in one place. |
| **Extensions** | Add integrations and feature extensions. |
| **Automation** | Rules, buttons, and scheduled commands. |
| **Permissions** | Public, private (workspace), private (invite-only). |
| **Views** | Board (Kanban), Timeline (Gantt), Calendar, Table, Dashboard, Map. |
| **Star / Watch** | Mark a board as a favorite; subscribe to its notifications. |

## 4. List-level features

| Feature | Description |
|---|---|
| **Position** | Lists have an order, can be dragged. |
| **Archive list** | Move list (and all its cards) to the archive. |
| **Move all cards** | Bulk action to move all cards in a list to another list. |
| **Subscribe** | Get notifications on every change in a list. |
| **List limits** (extension) | Cap the number of cards; turn the list red when over. |

## 5. Workspace features

| Feature | Description |
|---|---|
| **Boards** | Container for boards. |
| **Members** | Workspace members with roles (admin / member). |
| **Templates** | Workspace-level board templates visible to all members. |
| **Visibility** | Workspace visibility (private, public). |
| **Org-wide policies** | Security and compliance policies enforced org-wide. |
| **Org-wide permissions** | SAML, SCIM, audit logs, data residency. |

## 6. Comments

| Feature | Description |
|---|---|
| **Markdown** | Rich text with markdown shortcuts. |
| **Mentions** | @-mention a member, sends a notification. |
| **Reactions** | Emoji reactions on comments. |
| **Edits** | Comment history preserved. |
| **Delete** | Soft delete, preserved in activity log. |

## 7. Notifications

| Feature | Description |
|---|---|
| **In-app** | Bell icon with unread count. |
| **Email** | Per-event digest or immediate. |
| **Push** | Mobile push notifications. |
| **Per-resource watch** | Watch a card / list / board. |
| **Per-resource un-watch** | Stop receiving notifications. |

## 8. Search

| Feature | Description |
|---|---|
| **Global search** | Across all boards the user has access to. |
| **Card-level search** | Within a board, filter by text. |
| **Operator syntax** | `label:urgent @me due:overdue` style operators. |
| **Saved searches** | Save a filter as a named search. |

## 9. Automation engine

| Feature | Description |
|---|---|
| **Rules** | "When X happens, do Y." Triggered by board events. |
| **Custom buttons** | Add a button to a card. Pressing it runs actions. |
| **Scheduled commands** | Cron-like, run actions on a schedule. |
| **Card buttons** | Buttons shown on the card back. |
| **Board buttons** | Buttons shown on the board view. |
| **Built-in actions** | Move, copy, archive, add label, assign member, post comment, set due date, mark complete, etc. |
| **Quotas** | Per-user quota (configurable; default 250 runs / month). |

## 10. Extensions (integrations)

### 10.1 First-party (Cardscape-maintained)

- **Calendar** — Monthly calendar view of due dates.
- **Card Aging** — Fade stale cards.
- **Card Repeater** — Recurring cards.
- **Card Snooze** — Hide until a date.
- **Dashcards** — Card counters (overdue, by member, etc.).
- **List Limits** — Cap cards per list.
- **Voting** — Member voting on cards.
- **Custom Fields** — Dropdowns, dates, numbers, text.
- **Slack** — Link Slack channels to boards.
- **Google Drive** — Attach Drive files with live previews.
- **OneDrive** — Attach OneDrive files.
- **Dropbox** — Attach Dropbox files.
- **Box** — Attach Box files.
- **AI** — AI-generated descriptions, summaries, smart fields.

### 10.2 Third-party (most-requested)

- **GitHub** — Link branches, PRs, issues to cards.
- **GitLab** — Same as GitHub.
- **Microsoft Teams** — Channel notifications.
- **Outlook / Gmail** — Card from email.
- **Figma** — Embed Figma designs.
- **Miro** — Embed boards.
- **Loom** — Embed videos.
- **Zapier** — Connect to thousands of apps.
- **Make** — Alternative to Zapier.
- **Toggl / Harvest / Hubstaff** — Time tracking.
- **iCalendar** — Subscribe to board changes.
- **Webhooks** — Outgoing POST on any board event.

## 11. AI features

| Feature | Description |
|---|---|
| **Card description generation** | From a one-line title, write a draft description. |
| **Comment summary** | Summarize long comment threads. |
| **Smart Boards** | AI suggests task prioritization and delegation. |
| **Auto-checklists** | Generate a checklist from the description. |
| **Text improvement** | Rewrite / shorten / expand. |
| **AI card cover** | Generate a cover image from the card title. |

## 12. Inbox & Planner

| Feature | Description |
|---|---|
| **Inbox** | Personal task capture outside any board. |
| **Planner** | Personal calendar / list of cards. |
| **Voice capture** | Add to inbox by voice. |
| **Google Calendar sync** | Cards with due dates show in the user's calendar. |

## 13. Mobile

| Feature | Description |
|---|---|
| **Responsive web** | Layout down to phone width. |
| **PWA** | Installable, offline shell. |
| **Push notifications** | Native push. |
| **Widgets** | Home-screen widgets (PWA / native). |

## 14. Authentication & security

| Feature | Description |
|---|---|
| **Email / password** | Standard auth. |
| **OAuth (Google, Microsoft, Apple)** | Federated login. |
| **SSO (SAML / OIDC)** | Enterprise. |
| **Two-factor authentication** | TOTP-based. |
| **Org-wide security policies** | Workspace-level policy enforcement. |
| **API tokens** | Personal access tokens for the REST API. |
| **Audit logs** | Every action with who, what, when. |

## 15. Developer-facing

| Feature | Description |
|---|---|
| **REST API** | First-class REST API (subject of `api/00-conventions.md`). |
| **Webhooks** | Outgoing POSTs on board events. |
| **OAuth for third-party apps** | Token-based, scope-controlled. |
| **Extension framework** | Build your own extension. |
| **MCP server** | Model Context Protocol server (see ADR 0002). |

## 16. Source landscape

The shape of the inventory above is informed by the kanban and
project-management tooling landscape in 2026, including the
open-source self-hostable projects in the same niche, the SaaS
incumbents, and the maintainer's own product judgment. It is not
derived from any single competitor's feature matrix.

---

Next: see [`01-implementation-plan.md`](01-implementation-plan.md)
for the phased delivery plan that turns this inventory into
work.
