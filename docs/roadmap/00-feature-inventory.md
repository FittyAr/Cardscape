#  features — analysis

> The feature inventory Cardscape must match (or exceed) to be a
> credible open-source  alternative. Sources:
> https://trello.com/features, public marketing pages, third-party
> 2026 reviews, and the Atlassian /  product pages.

This is a **scraped inventory** of what  offers as of July
2026, not a feature spec for Cardscape. The implementation plan
that turns this into a delivery schedule is in
[`01-implementation-plan.md`](01-implementation-plan.md).

---

## 1. Core model

| Feature | Description | Source |
|---|---|---|
| **Workspaces** | Top-level container that groups boards and members |  help center |
| **Boards** | The kanban surface. Holds lists, members, labels, extensions. Has a name, description, background, visibility. |  help center |
| **Lists** | Columns within a board. Ordered, can be archived. Hold cards. |  help center |
| **Cards** | The atomic unit. Title, description, members, labels, due date, attachments, comments, checklists, custom fields, cover image, activity. |  help center |
| **Members / Users** | Profile (name, avatar, email, initials). Assigned to cards, boards, workspaces. |  help center |

## 2. Card-level features

| Feature | Description |
|---|---|
| **Title** | Required, ≤ 512 chars. |
| **Description** | Long-form text with rich-text editor (mentions, attachments, formatting). |
| **Members / assignees** | One or more users assigned to the card. |
| **Due date** | Optional date with optional time. Reminders configurable. |
| **Labels** | Color-coded tags per board. |
| **Checklists** | Subtasks on the card. Each item has a name, optional due date, optional assignee, completed state. |
| **Attachments** | Files up to 250 MB on paid plans; links with previews. |
| **Comments** | Conversation on the card, with mentions, reactions, edits. |
| **Activity** | Append-only log of every change to the card. |
| **Cover** | Image or color used as the visual background. |
| **Custom fields** | Paid feature. Dropdowns, dates, numbers, text — schema defined per board. |
| **Card mirror** | Same card linked across multiple boards. |
| **Card aging** | Free power-up. Visually fades cards that haven't seen activity. |
| **Card snooze** | Free power-up. Hide the card until a date/time. |
| **Card repeater** | Free power-up. Create a copy of the card on a schedule. |
| **Voting** | Free power-up. Members add votes to cards. |
| **Watch** | Subscribe to a card's notifications. |

## 3. Board-level features

| Feature | Description |
|---|---|
| **Drag and drop** | Move cards within and across lists. Reorder lists. |
| **Board templates** | Pre-built boards (Kanban, Scrum, Calendar, etc.). Free, with many categories. |
| **Board filtering** | Filter cards by member, label, due date, custom field. |
| **Board background** | Color or image (including uploaded custom images on paid). |
| **Archive** | Archive cards and lists. Board-level archive accessible from a sidebar. |
| **Activity log** | All changes to the board in one place. |
| **Power-Ups** | Add integrations and feature extensions. |
| **Automation automation** | Rules, buttons, and scheduled commands. |
| **Permissions** | Public, private (workspace), private (invite-only). |
| **Views** | Board (Kanban), Timeline (Gantt), Calendar, Table, Dashboard, Map. Most views are paid. |
| **Star / Watch** | Mark a board as a favorite; subscribe to its notifications. |

## 4. List-level features

| Feature | Description |
|---|---|
| **Position** | Lists have an order, can be dragged. |
| **Archive list** | Move list (and all its cards) to the archive. |
| **Move all cards** | Bulk action to move all cards in a list to another list. |
| **Subscribe** | Get notifications on every change in a list. |
| **List limits** (power-up) | Cap the number of cards; turn the list red when over. |

## 5. Workspace features

| Feature | Description |
|---|---|
| **Boards** | Container for boards. |
| **Members** | Workspace members with roles (admin / member). |
| **Templates** | Workspace-level board templates visible to all members. |
| **Visibility** | Workspace visibility (private, public). |
| **org-wide security policies** (enterprise) | Security policies enforced org-wide. |
| **Org-wide permissions** (enterprise) | SAML, SCIM, audit logs, data residency. |

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

## 9. Automation automation

| Feature | Description |
|---|---|
| **Rules** | "When X happens, do Y." Triggered by board events. |
| **Custom buttons** | Add a button to a card. Pressing it runs actions. |
| **Scheduled commands** | Cron-like, run actions on a schedule. |
| **Card buttons** | Buttons shown on the card back. |
| **Board buttons** | Buttons shown on the board view. |
| **List of built-in actions** | Move, copy, archive, add label, assign member, post comment, set due date, mark complete, etc. |
| **Quotas** | 250 runs / month free, unlimited paid. |

## 10. Power-Ups (integrations)

### 10.1 First-party (-maintained)

- **Calendar** — Monthly calendar view of due dates.
- **Card Aging** — Fade stale cards.
- **Card Repeater** — Recurring cards.
- **Card Snooze** — Hide until a date.
- **Dashcards** — Card counters (overdue, by member, etc.).
- **List Limits** — Cap cards per list.
- **Voting** — Member voting on cards.
- **Custom Fields** (paid) — Dropdowns, dates, numbers, text.
- **Slack** — Link Slack channels to boards.
- **Google Drive** — Attach Drive files with live previews.
- **OneDrive** — Attach OneDrive files.
- **Dropbox** — Attach Dropbox files.
- **Box** — Attach Box files.
- **Atlassian Intelligence** (paid) — AI-generated descriptions,
  summaries, smart fields.

### 10.2 Third-party (most-used)

- **Jira Cloud** — Link  cards to Jira issues.
- **Confluence** — Embed Confluence pages in cards.
- **Bitbucket** — Link PRs to cards.
- **GitHub** — Link branches, PRs, issues to cards.
- **GitLab** — Same as GitHub.
- **Microsoft Teams** — Channel notifications.
- **Outlook / Gmail** — Card from email.
- **Salesforce** — Sync accounts / opportunities.
- **HubSpot** — Marketing / CRM.
- **Mailchimp** — Campaigns on the board.
- **Figma** — Embed Figma designs.
- **Miro** — Embed boards.
- **Loom** — Embed videos.
- **Zapier** — Connect to 5000+ apps.
- **Make** (Integromat) — Alternative to Zapier.
- **Toggl / Harvest / Hubstaff** — Time tracking.
- **iCalendar** — Subscribe to board changes.
- **Webhooks** — Outgoing POST on any board event.

## 11. AI features (Atlassian Intelligence, paid)

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
| **iOS app** | Full feature parity with the web app. |
| **Android app** | Same. |
| **Offline mode** | View and edit cards offline; sync on reconnect. |
| **Push notifications** | Native push. |
| **Widgets** | iOS / Android home-screen widgets. |

## 14. Authentication & security

| Feature | Description |
|---|---|
| **Email / password** | Standard auth. |
| **OAuth (Google, Microsoft, Apple)** | Federated login. |
| **SSO (SAML / OIDC)** | Enterprise. |
| **Two-factor authentication** | TOTP-based. |
| **org-wide security policies** (enterprise) | Org-wide security policies. |
| **API tokens** | Personal access tokens for the REST API. |
| **Audit logs** (enterprise) | Every action with who, what, when. |

## 15. Developer-facing

| Feature | Description |
|---|---|
| **REST API** | First-class REST API (subject of these conventions). |
| **Webhooks** | Outgoing POSTs on board events. |
| **OAuth for third-party apps** | Token-based, scope-controlled. |
| **Power-Up framework** | Build your own Power-Up. |

## 16. Pricing tiers (for context, not for our spec)

| Plan | Boards | Power-Ups / board | File size | Automations / month |
|---|---|---|---|---|
| **Free** | 10 per workspace | unlimited | 10 MB | 250 |
| **Standard** | unlimited | unlimited | 250 MB | 1,000 |
| **Premium** | unlimited | unlimited | 250 MB | unlimited |
| **Enterprise** | unlimited | unlimited | 250 MB | unlimited |

Self-hosting is **not** a  tier; it's a Cardscape value
proposition.

## 17. Source list

-  product pages and help center
- https://www.taskrhino.ca/blog/trello-review/
- https://www.sendboard.com/blog/best-free-trello-extensions
- https://www.smartsuite.com/blog/trello-review
- https://saasrat.com/products/trello
- https://match-vs.com/en/tool/trello
- https://www.techstackdaily.com/review/trello-review-2026/

---

Next: see [`01-implementation-plan.md`](01-implementation-plan.md)
for the phased delivery plan that turns this inventory into
work.
