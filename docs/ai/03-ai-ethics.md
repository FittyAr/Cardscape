# AI ethics

> The project's stance on AI: what we build, what we do
> not build, who pays, who decides, and how the user
> stays in control. The stance is a **values** document,
> not a technical spec; the spec is in
> [`docs/design/02-logging-observability.md`](../design/02-logging-observability.md),
> [`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md),
> and the ADR 0002.
>
> The project is solo-maintained. This document is the
> maintainer's commitment to the community. When the
> project grows to multi-maintainer, this document is
> ratified by the maintainers per
> [docs/community/GOVERNANCE.md](../../docs/community/GOVERNANCE.md).

---

## 1. The principle

AI is a **tool the user controls**, not a **decision-maker
the user is subject to**. The user is the human in the
loop; the AI is the assistant. Every AI feature in
Cardscape is designed so that:

- The user **initiates** the AI action (the user asks the
  question, the user approves the action).
- The user can **override** the AI's suggestion (the AI
  recommends; the user decides).
- The user can **disable** the AI entirely (the AI is
  opt-in, not opt-out).
- The user can **audit** the AI's actions (every AI
  action is logged with the user, the action, and the
  outcome).

This principle is the bedrock. Every decision below
follows from it.

---

## 2. What Cardscape builds (the AI feature surface)

Cardscape builds AI features that are **assistive, not
autonomous**. The AI helps the user; the user decides.

| Feature | What the AI does | What the AI does not do |
|---|---|---|
| **Card description generation** | drafts a description from a one-line title | post the description without the user's approval |
| **Comment summary** | summarizes a long comment thread | replace the thread with the summary |
| **Auto-checklists** | suggests a checklist from the description | create the checklist without the user's approval |
| **Smart Boards** | suggests task prioritization and delegation | reorder the board or reassign cards |
| **Text improvement** | rewrites, shortens, or expands the user's text | replace the user's text without approval |
| **AI card cover** | generates a cover image from the card title | set the cover without the user's approval |
| **MCP-driven actions** | the AI reads boards, creates cards, moves cards | the AI's actions are scoped to the API token's scopes; the user can revoke the token at any time |

Every AI feature has a "the user approves" step. The
exception is the MCP-driven actions, where the user has
already approved the scope of the API token (e.g.
"the AI can read all my boards"); the user can revoke
the token at any time, which revokes all the AI's
access.

---

## 3. What Cardscape does not build

Cardscape deliberately does **not** build:

- **AI that acts on the user's behalf without the user's
  approval.** The AI is a tool; the user is the actor.
- **AI that infers things about the user that the user has
  not explicitly shared.** The AI works with the data in
  Cardscape; it does not infer "the user is X" from
  "the user has Y cards in this list".
- **AI that trains on the user's data.** The AI is a
  remote service (or a local model, if the user chooses);
  the user's data is sent to the AI provider only when
  the user explicitly asks (e.g. "summarize this
  comment"). The user's data is not used to train the
  AI provider's models.
- **AI that decides what the user should see.** The board
  view shows the cards the user has access to. The AI
  does not filter, rank, or hide cards based on what it
  thinks the user should see.
- **AI that monitors the user.** Cardscape is a
  productivity tool, not a surveillance tool. The AI
  does not track the user's behavior beyond what is
  needed to provide the AI feature.
- **AI that generates content the user did not ask for.**
  The AI does not auto-generate comments, auto-fill
  fields, or auto-create cards without the user's
  explicit request.

---

## 4. The AI provider model

The AI features in Cardscape are powered by an
**AI provider**. The provider is a separate OpenAI-compatible service
(OpenAI, a compatible gateway, or a local model) that the user
configures. Cardscape does not ship an AI model; the
project ships the **integration** with the provider.

### 4.1 The provider is the user's choice

The user can choose:

- **A hosted provider** (OpenAI, Anthropic, etc.). The
  user provides an API key; Cardscape calls the provider's
  API on the user's behalf. The user's data is sent to
  the provider.
- **A local model** (Ollama, LM Studio, etc.). The user
  runs the model on the same host as Cardscape. The
  user's data does not leave the host.

The default endpoint is local Ollama at
`http://localhost:11434/` with model `llama3.2`. Operators using a hosted
provider override `Ai:Endpoint`, `Ai:Model` and `Ai:ApiKey`. Cardscape never
substitutes templates or simulated AI when the provider is unavailable; the
requested operation fails as an external dependency error.

### 4.2 The provider is the user's data, not Cardscape's

Cardscape does not store the user's API key in a database
or a config file that is sent to a Cardscape-controlled
service. The key is stored in the user's environment
(e.g. `Ai__ApiKey`) or in the user's
secret manager (e.g. HashiCorp Vault, AWS Secrets
Manager). The key is read at startup and held in memory;
it is not logged, not exported, and not sent to any
service other than the provider.

### 4.3 The provider's data policy is the user's problem

Cardscape does not negotiate the data policy with the
provider. The user chooses the provider; the user is
responsible for understanding the provider's data
policy. Cardscape documents the providers it integrates
with in
[`docs/ai/01-mcp-deep-dive.md`](01-mcp-deep-dive.md) and
flags the data-sensitive ones (e.g. providers that train
on user data).

---

## 5. The AI's data boundaries

The AI sees **what the API token's scopes allow**. The
same authorization pipeline that protects the REST API
protects the MCP server (see
[`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md)
§7). An AI client that has been granted the `cards:read`
scope can read the user's cards; an AI client that has
been granted the `cards:write` scope can create, update,
move, and archive cards; an AI client that has been
granted the `comments:write` scope can add comments.

The AI does **not** see:

- The user's password (the AI never authenticates as the
  user; it authenticates with an API token).
- The user's email (the email is metadata; the AI does
  not need it).
- The user's other workspaces (the AI is scoped to the
  workspace the API token is for).
- Other users' data (the AI is scoped to the API token's
  user).

The AI **does** see:

- The cards, lists, boards, and workspaces the API token
  is scoped to.
- The comments on those cards.
- The members of those workspaces (display names, not
  emails).
- The activity stream on those cards (the who, the what,
  the when).

The user can revoke the API token at any time, which
revokes all of the AI's access.

---

## 6. The AI's actions are auditable

Every AI action is logged in the `AuditLog` (see
[`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md)
§6). The log entry carries:

- The actor (the API token, which is owned by the user).
- The action (the tool call: `cards_create`, `cards_move`,
  etc.).
- The target (the card, the board, etc.).
- The parameters (the title, the description, the new
  list, etc.).
- The result (success or failure, with the error code if
  applicable).
- The timestamp and the trace id.

The audit log is **append-only** and is never edited or
deleted. The retention is 7 years (the SOC 2 default;
the user can configure a shorter retention for
non-regulated deployments).

The user can review the audit log at any time. The
"Settings → Activity" page in the web UI shows the AI's
actions in the same view as the human users' actions.

---

## 7. The user controls AI invocation

Cardscape never invokes the provider automatically. Every generation or
summary begins with an explicit user action in Radzen or an authenticated MCP
tool call. API-token scopes and revocation control MCP access. A deployment
that must prohibit AI entirely should block the configured provider endpoint;
a first-class deployment/workspace feature toggle is not currently shipped.

---

## 8. The AI does not make decisions for the user

The AI **suggests**; the user **decides**. The
implementation:

- The AI's output is a **suggestion**, displayed in the
  web UI as a "AI suggested..." block. The user can
  accept, modify, or reject the suggestion.
- The AI's actions over MCP are **confirmed** by the user
  in the AI client (Claude Desktop, Cursor, etc.) before
  they are executed. The AI client shows the action and
  asks the user to confirm.
- The AI's automated actions (e.g. a rule that calls an
  AI provider) are **scoped to the rule author's
  permissions**. A user cannot write a rule that does
  something the user could not do manually (see
  [`docs/security/01-threat-model.md`](../security/01-threat-model.md)
  §6).

The "the AI decides" pattern is **explicitly rejected**.
The user is the human in the loop.

---

## 9. The AI is not a person

The AI is a tool, not a person. The AI's outputs are
not "the AI's opinion" or "the AI's recommendation" in
the social sense; they are the model's prediction given
the input. The user is responsible for the AI's outputs
in the same way the user is responsible for the outputs
of any other tool the user uses.

Cardscape does not anthropomorphize the AI. The UI
refers to the AI as "the AI" or "the model", not as
"Cardscape AI" or "your AI assistant" (the brand
"Cardscape AI" is a product name, not a persona).

---

## 10. The AI's biases are the user's problem

The AI model is trained on data the user did not choose
(the model's training data). The model's biases are the
model's biases; Cardscape does not train the model, does
not fine-tune the model, and does not control the model's
outputs. The user is responsible for evaluating the
model's outputs in the context of the user's own values
and the user's own work.

Cardscape does not make claims about the model's
accuracy, fairness, or appropriateness. The user is
responsible for those evaluations.

---

## 11. The AI is not a replacement for the human

The AI helps the user be more productive. The AI is **not
a replacement** for the user's judgment, the user's
expertise, or the user's relationships. The user is the
human in the loop; the AI is the assistant.

When the AI's output would have a significant impact
(e.g. "send this email to 1000 users", "delete this
board", "revoke all API tokens"), the user is asked to
confirm. The AI does not perform the action without the
user's explicit approval.

---

## 12. The AI is open about its limitations

The AI feature's documentation (in
[`docs/ai/01-mcp-deep-dive.md`](01-mcp-deep-dive.md) and
[`docs/ai/02-prompt-library.md`](02-prompt-library.md))
documents the AI's limitations:

- The AI is **probabilistic**; the same prompt can produce
  different outputs.
- The AI is **not always correct**; the user is responsible
  for verifying the AI's outputs.
- The AI is **bounded by the API token's scopes**; the
  AI cannot do what the user has not authorized.
- The AI is **not a search engine**; the AI's training
  data has a cutoff, and the AI does not have access to
  the internet (unless the user has configured a tool
  that does).

The documentation is honest about the AI's limitations.
The user is not oversold on the AI's capabilities.

---

## 13. The AI is not a surveillance tool

Cardscape is a productivity tool, not a surveillance
tool. The AI does not:

- Track the user's behavior beyond what is needed to
  provide the AI feature.
- Report the user's behavior to anyone (the user, the
  workspace admins, the maintainer, the provider).
- Use the user's behavior to build a profile of the user.
- Sell the user's behavior to third parties.

The AI's audit log is a record of the AI's actions, not
a record of the user's behavior. The audit log is
visible to the user and to the workspace admins; it is
not visible to the maintainer, the provider, or anyone
else.

---

## 14. When to revisit

This document is revisited when:

1. A new AI feature is added (the feature is reviewed
   against the principles in §1).
2. A new AI provider is supported (the data policy is
   documented in §4).
3. A real user reports an ethical concern (the concern is
   addressed in this document).
4. The project's stance changes (a new ADR is added that
   supersedes this document).

Until then, this document is the source of truth for the
project's stance on AI ethics in Cardscape.
