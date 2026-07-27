# Prompt library

> The canonical prompts that ship with the MCP server. A
> prompt is a templated instruction the AI client can run
> against the user's Cardscape data. The library is the
> reference implementation for the project's most useful
> AI-driven workflows.
>
> A prompt is **a starting point, not a finished product**.
> The user can edit the prompt before running it; the
> AI client (Claude Desktop, Cursor, etc.) renders the
> prompt in the chat and the user can refine it.

---

## 1. The library's design

Every prompt in the library:

- **Has a clear name** (e.g. `standup-summary`,
  `triage-inbox`).
- **Has a one-line description** that the AI client shows
  in the prompt list.
- **Accepts parameters** with defaults.
- **Returns a rendered prompt** (a string) that the AI
  client sends to the model.
- **Is implemented as a C# method** on a class registered
  with the MCP server. See
  [`01-mcp-deep-dive.md`](01-mcp-deep-dive.md) §5.
- **Is tested** in the MCP server's test suite.
- **Is documented** in this file (the section below).

A new prompt is added in three steps:

1. **The C# method** in
   `src/Cardscape.Mcp/Prompts/<Name>Prompt.cs`.
2. **The unit test** in
   `tests/Cardscape.UnitTests/Prompts/<Name>PromptTests.cs`.
3. **The documentation** in this file (a new section in
   §3 below).

---

## 2. The naming convention

The prompt name is `kebab-case`. The name is what the
user types to invoke the prompt in the AI client
(e.g. `/standup-summary` in Claude Desktop). The name
should be:

- **Action-oriented** (`standup-summary`, `triage-inbox`,
  `sprint-planning`).
- **Specific** (not `summary` — too generic;
  `standup-summary` is specific).
- **Lowercase** (kebab-case, all lowercase, no
  CamelCase).

A new prompt name that conflicts with an existing prompt
is rejected in review.

---

## 3. The library

### 3.1 `standup-summary`

**What it does**: produces a standup summary for the cards
assigned to the user that were touched in the last 24
hours.

**When to use it**: every morning, before the standup
meeting.

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `hours_back` | int | `24` | how many hours back to look |
| `include_comments` | bool | `true` | include the latest comment on each card |

**The rendered prompt**:

```text
You are helping me prepare for my daily standup. Here are
the cards assigned to me that were touched in the last
{hours_back} hours:

{for each card}
- [{card.id}] {card.title} (on {card.board_name} / {card.list_name})
  Status: {card.status}
  Latest comment: {card.latest_comment}
{end for}

Please produce a standup summary in the "Yesterday /
Today / Blockers" format. Be specific (cite card ids and
titles). Be brief (under 200 words).
```

### 3.2 `triage-inbox`

**What it does**: helps the user triage the cards in
their Inbox (the cards captured outside any board).

**When to use it**: Monday morning, or whenever the
Inbox has more than 10 cards.

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `max_cards` | int | `20` | the maximum number of cards to triage |
| `board_options` | list of strings | the user's recent boards | the boards the user can move cards to |

**The rendered prompt**: see the example in
[`01-mcp-deep-dive.md`](01-mcp-deep-dive.md) §5.

### 3.3 `sprint-planning`

**What it does**: helps the user plan the next sprint from
the Backlog list of the active board.

**When to use it**: at the start of a new sprint.

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `board_id` | guid | the user's active board | the board to plan for |
| `sprint_length_days` | int | `14` | the length of the sprint |
| `team_capacity_hours` | int | (required) | the team's capacity in hours |

**The rendered prompt**:

```text
You are helping me plan the next sprint on
{board_name}. The sprint is {sprint_length_days} days
long and the team has {team_capacity_hours} hours of
capacity.

Here are the cards in the Backlog list, sorted by the
priority I assigned:

{for each card, sorted by priority}
- [{card.id}] {card.title}
  Estimate: {card.estimate_hours}h
  Labels: {card.labels}
  Dependencies: {card.dependencies}
{end for}

Please:
1. Select the cards that fit in the sprint, respecting
   the team capacity and the dependencies.
2. Identify the cards that should be split or
   descoped.
3. Identify the risks (cards with no estimate, cards
   blocked on external dependencies, etc.).

Output a sprint plan as a markdown table.
```

### 3.4 `weekly-review`

**What it does**: summarizes the week's activity on the
user's boards.

**When to use it**: every Friday afternoon, or at the end
of the week.

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `days_back` | int | `7` | how many days back to look |

**The rendered prompt**:

```text
You are helping me do a weekly review. Here is the
activity on my boards in the last {days_back} days:

- Cards created: {count_created}
- Cards moved to Done: {count_done}
- Cards archived: {count_archived}
- Comments added: {count_comments}
- Cards assigned to me: {count_assigned_to_me}
- Cards I assigned: {count_assigned_by_me}

Top 5 cards by activity:
{for each top card}
- [{card.id}] {card.title} — {card.activity_count} events
{end for}

Stale cards (no activity in 14+ days, not archived):
- (list up to 10)

Please:
1. Summarize the week in 3-5 sentences.
2. Highlight what went well and what didn't.
3. Suggest 2-3 things to focus on next week.
```

### 3.5 `stale-cards`

**What it does**: finds the cards that have not been
touched in N days, across the user's boards.

**When to use it**: during the weekly review, or whenever
the user wants to clean up.

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `days_threshold` | int | `14` | the staleness threshold in days |
| `boards` | list of guids | all the user's boards | the boards to search |

**The rendered prompt**:

```text
You are helping me find stale cards. A card is "stale" if
it has not been touched in {days_threshold} days and is
not in an archive list.

Here are the stale cards:

{for each card, sorted by oldest activity}
- [{card.id}] {card.title} (on {board_name} / {list_name})
  Last activity: {card.last_activity_at} ({card.days_since_activity} days ago)
{end for}

Please suggest, for each card:
- **Archive**: it's not relevant anymore.
- **Move to backlog**: it's still relevant but not
  current.
- **Snooze until**: defer it to a specific date.
- **Keep as is**: it's a long-running card by design.
```

### 3.6 `card-template`

**What it does**: generates a card from a template the
user provides.

**When to use it**: when the user wants to create many
similar cards (e.g. a checklist of tasks for a recurring
meeting).

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `template` | string | (required) | the template, with `{placeholder}` syntax |
| `list_id` | guid | (required) | the list to create the cards in |
| `data` | object | (required) | the data to fill the template with |

**The rendered prompt**:

```text
You are helping me create cards from a template.

Template:
{template}

Data:
{data}

For each entry in the data, create a card by substituting
the placeholders in the template. Use the
`cards_create` tool. Return the list of created card ids.
```

### 3.7 `triage-comments`

**What it does**: summarizes the unread @mentions and
comments on the user's cards.

**When to use it**: when the user has been away for a
while and wants to catch up on the conversation.

**Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `hours_back` | int | `48` | how many hours back to look |
| `only_mentions` | bool | `false` | only @mentions of the user |

**The rendered prompt**:

```text
You are helping me catch up on the conversation. Here are
the comments on my cards in the last {hours_back} hours
{only_mentions: " (only @mentions of me)"}:

{for each comment}
- [{comment.id}] on [{card.id}] {card.title}
  By {comment.author} at {comment.created_at}:
  "{comment.text}"
{end for}

Please:
1. Summarize the key points in 3-5 sentences.
2. Identify the comments that need a response from me
   (questions, blockers, requests).
3. Identify the comments that are FYI and don't need a
   response.
```

---

## 4. The library's design principles

The prompts in the library follow these principles:

- **Specific, not generic.** A prompt that says "help me
  with my cards" is not useful. A prompt that says
  "summarize the cards assigned to me that were touched
  in the last 24 hours in the 'Yesterday / Today /
  Blockers' format" is useful.
- **Structured output.** The prompt tells the AI to output
  a table, a list, or a specific format. Unstructured
  output is harder to act on.
- **Cite the data.** The prompt tells the AI to cite card
  ids and titles, not paraphrase. The user can then act
  on the AI's output with the `cards_*` tools.
- **Bounded scope.** A prompt that asks for a 200-word
  summary gets a 200-word summary. A prompt that asks for
  a "comprehensive analysis" gets an unmaintainable
  response.
- **User-editable.** The prompt is a starting point. The
  user can edit it before running. The prompt is not a
  "the AI is in charge" message; the user is in charge.

---

## 5. Adding a new prompt

The recipe. See
[`01-mcp-deep-dive.md`](01-mcp-deep-dive.md) §5 for the
mechanical steps (C# method, registration, test). The
documentation step is:

1. Add a new section to §3 of this file. The section
   includes:
   - What it does (one sentence).
   - When to use it (one sentence).
   - Parameters (a table).
   - The rendered prompt (a code block).
2. Update the §4 principles if the new prompt introduces
   a new pattern.
3. Update the §2 naming convention if the new prompt's
   name is in a new namespace (rare; the convention is
   stable).

A new prompt without documentation is rejected in review.

---

## 6. The end-to-end smoke test

The library has a smoke test that runs every prompt
against a seeded workspace. The smoke test:

1. Seeds a workspace with 3 boards, 10 lists, 50 cards,
   100 comments, 5 members.
2. For each prompt in the library:
   - Renders the prompt with the default parameters.
   - Asserts the rendered prompt is not empty.
   - Asserts the rendered prompt contains the expected
     placeholders filled in.
3. Runs the prompts against a real AI model (the maintainer
   uses Claude 3.5 Sonnet; the test harness uses a mock
   that records the rendered prompt and the response).
4. Asserts the response is in the expected format
   (table, list, etc.).

The smoke test runs in the CI. A prompt that fails the
smoke test is not merged.

---

## 7. The community prompt library

A future PR (Phase 5+) may add a `prompts/community/`
folder for community-contributed prompts. The folder
follows the same structure as the canonical library but
the prompts are not part of the official server image;
they are loaded from a separate file or a separate repo.

Until then, the community can share prompts in
[GitHub Discussions → Show and tell](https://github.com/cardscape/cardscape/discussions/categories/show-and-tell).
The maintainer promotes a community prompt to the
canonical library when it has been tested by at least 3
users and the maintainer reviews it.

---

## 8. When to revisit

This document is revisited when:

1. A new prompt is added to the library.
2. A new pattern is introduced (e.g. multi-step prompts
   that call multiple tools in sequence).
3. A new AI model is supported (the prompts are tuned
   for the model's strengths; e.g. Claude 3.5 Sonnet vs
   GPT-4o vs Gemini 1.5 Pro).
4. A real user reports that a prompt is not useful as-is
   (the prompt is updated based on the feedback).

Until then, this document is the source of truth for the
prompt library in Cardscape.
