# 02 — Importing from Trello

> How to bring a Trello workspace into Cardscape
> end-to-end. Covers the JSON shape, the
> `POST /api/imports/trello` endpoint, the dry-run
> preview mode, the MCP tools, and the mapping rules.

---

## 1. The short version

Trello lets users export a workspace as a single
`boards.json` file (Trello → Profile → Settings →
"Print and Export" → "JSON"). Cardscape reads that
file as-is and creates a matching set of
workspaces / boards / lists / cards / labels /
members under a target Cardscape workspace of the
caller's choice.

Three ways to drive the import:

1. **Web UI** — `Workspaces / <id> / Import`. File
   picker, submit, see the live preview, submit
   again to apply.
2. **REST API** — `POST /api/imports/trello`
   (multipart upload). Same preview / apply
   semantics, controlled by a `previewOnly`
   form field.
3. **MCP** — `imports_trello_preview` and
   `imports_trello_apply` tools. The preview
   tool is a dry-run; the apply tool writes to
   the database.

## 2. JSON shape

Trello's `boards.json` is a JSON array of Trello
board objects. Cardscape only reads the fields
it needs; unknown fields are silently dropped.

```json
[
  {
    "id": "5d9...e8",
    "name": "Personal board",
    "desc": "The board description",
    "lists": [
      { "id": "5da...01", "name": "Todo", "closed": false },
      { "id": "5da...02", "name": "Doing", "closed": false },
      { "id": "5da...03", "name": "Done",  "closed": true  }
    ],
    "cards": [
      {
        "id": "5db...c1",
        "name": "First card",
        "desc": "Body of the first card",
        "idList": "5da...01",
        "closed": false,
        "due": "2026-09-01T12:00:00.000Z",
        "labels": [
          { "id": "5dc...01", "name": "bug",     "color": "red"    },
          { "id": "5dc...02", "name": "feature", "color": "green"  }
        ],
        "idMembers": ["5dd...01", "5dd...02"]
      }
    ],
    "labels": [
      { "id": "5dc...01", "name": "bug",     "color": "red"    },
      { "id": "5dc...02", "name": "feature", "color": "green"  }
    ],
    "members": [
      { "id": "5dd...01", "username": "alice", "fullName": "Alice Anderson", "email": "alice@example.com" },
      { "id": "5dd...02", "username": "bob",   "fullName": "Bob Brown",      "email": "bob@example.com"   }
    ]
  }
]
```

The array may contain more than one board; each
Trello board becomes one Cardscape board inside
the target workspace.

## 3. REST endpoint

```
POST /api/imports/trello
Content-Type: multipart/form-data; boundary=...

(file=<boards.json>; targetWorkspaceId=<guid>; previewOnly=true|false)
```

- `file` — the `boards.json` upload. Required.
  The endpoint reads the stream into
  `IImportService.ImportTrelloJsonAsync`. Max
  size is the ASP.NET Core default per-file
  limit (≈128 MB out of the box; raise
  `KestrelServerOptions.Limits.MaxRequestBodySize`
  if you need more).
- `targetWorkspaceId` — the Cardscape workspace
  the import should land in. Required. The
  caller must have owner / admin role on the
  target workspace.
- `previewOnly` — optional. When `true`, the
  service parses the file and returns a
  populated `ImportPreview` summary but does
  **not** write to the database. When
  absent or `false`, the import is applied.
  Defaults to `false`.

### Response shape

```json
{
  "importedWorkspaceIds": [],
  "importedBoardIds":     ["7a1b...c3", "7a1b...c4"],
  "importedListIds":      ["7a1c...01", "7a1c...02", "7a1c...03", "..."],
  "importedCardIds":      ["7a1d...e1", "7a1d...e2", "..."],
  "importedLabelIds":     ["7a1e...01", "7a1e...02"],
  "preview": {
    "boardCount": 2,
    "listCount":  9,
    "cardCount":  47,
    "labelCount": 6,
    "memberCount":3,
    "sampleBoardNames": ["Personal board", "Work board"],
    "sampleListNames":  ["Todo", "Doing", "Done", "..."],
    "sampleCardNames":  ["First card", "Second card", "..."],
    "wasApplied": true
  }
}
```

`importedXxxIds` is empty on a dry-run.
`preview.wasApplied` is `false` on a dry-run
and `true` on an apply. The `sample*` arrays
are capped (5 board names, 10 list names, 20
card names) to keep the response small for
very large exports.

### Error envelope

Errors come back as JSON with a stable `error`
code:

| HTTP | `error` code | When |
|---|---|---|
| 400 | `imports.invalid_content_type` | The request body is not `multipart/form-data`. |
| 400 | `imports.invalid_workspace`   | The `targetWorkspaceId` field is missing or not a GUID. |
| 400 | `imports.no_file`             | The `file` part is missing or empty. |
| 400 | (parser-specific)             | The JSON is malformed, the array is empty, or a board references a list / label / member that does not exist in the file. |
| 401 | `auth.required`               | The bearer token is missing or invalid. |
| 403 | (workspace RBAC)              | The caller is not a member of `targetWorkspaceId`. |

## 4. Dry-run vs. apply semantics

`previewOnly=true` (or the `imports_trello_preview`
MCP tool) is the recommended first step. The
service:

1. Reads the JSON stream.
2. Builds the in-memory aggregate tree (boards,
   lists, cards, labels, members).
3. Returns the same `ImportResult` shape as the
   apply path, with the four `importedXxxIds`
   lists empty and `preview.wasApplied = false`.

This lets the caller show the user "this
import will create 2 boards, 9 lists, and 47
cards" before any write happens.

## 5. Mapping rules

The Trello ↔ Cardscape mapping is intentionally
lossy: Trello fields that have no Cardscape
counterpart are dropped silently. The reverse
also holds — Trello exports do not contain
custom fields, recurring cards, voting, or
board extensions, so those surfaces stay empty
in the imported data.

| Trello | Cardscape | Notes |
|---|---|---|
| `boards[]` | one `Board` per element | All boards land in the **same** target workspace. |
| `boards[].name` | `Board.Name` | Required. Empty / whitespace names abort the import with `imports.invalid_board_name`. |
| `boards[].desc` | `Board.Description` | Optional. Trello markdown is preserved as plain text. |
| `boards[].lists[]` (open) | one `BoardList` per element | `closed: true` lists are dropped. |
| `lists[].name` | `BoardList.Name` | Required. Empty / whitespace names abort the import. |
| `lists[].pos` | `BoardList.Position` | Trello floats are normalised to int. |
| `cards[]` (open) | one `Card` per element | `closed: true` cards are dropped. |
| `cards[].name` | `Card.Title` | Required. Empty / whitespace names abort the import. |
| `cards[].desc` | `Card.Description` | Optional. Trello markdown is preserved as plain text. |
| `cards[].due` | `Card.DueDate` | Optional. Parsed as ISO 8601; invalid dates abort the import. |
| `cards[].idList` | card ↔ list join | Must match a `lists[].id` in the same board. Orphaned cards abort the import. |
| `cards[].labels[]` | card ↔ label join | The label objects are auto-created if they are not in `boards[].labels[]`. |
| `cards[].idMembers` | card ↔ member join | Trello members without an email are dropped (Cardscape members must have an email). |
| `boards[].labels[]` | one `Label` per element | Named labels become Cardscape labels. `null`-named labels become "Unnamed label". |
| `boards[].members[]` | one `WorkspaceMember` per element | Members are added to the target workspace if their email matches an existing Cardscape user; otherwise they are recorded in the import preview but not added. |

## 6. MCP tools

The MCP server exposes two tools that wrap the
endpoint:

### `imports_trello_preview`

| | |
|---|---|
| Inputs | `boardsJsonPath` (string) — path on the MCP host filesystem to a Trello `boards.json` file. |
| Outputs | The `ImportResult` JSON above, with `wasApplied = false`. |
| Notes | Pure dry-run. The database is never touched. The MCP host reads the file (not the API server) and dispatches the call as `previewOnly=true`. |

### `imports_trello_apply`

| | |
|---|---|
| Inputs | `boardsJsonPath` (string), `targetWorkspaceId` (GUID). |
| Outputs | The `ImportResult` JSON above, with `wasApplied = true` and populated `importedXxxIds`. |
| Notes | Calls the same pipeline with `previewOnly=false`. The MCP client's API token must be authorized on the target workspace. |

The two tools share the same code path; the
only difference is the `previewOnly` flag.
The audit's "imports_trello_preview is
identical to imports_trello_apply" concern
from v1.1.0 is resolved as of v1.1.0 — the
`previewOnly` form field and the
`ImportResult.Preview.WasApplied` flag
distinguish the two paths.

## 7. Idempotency

Imports are **not** idempotent. Running the
same `boards.json` twice against the same
target workspace will create a duplicate
board / list / card tree. The service does
not dedupe on Trello IDs because Trello IDs
are Trello-scoped and Cardscape IDs are
Cardscape-scoped; the mapping is not a 1:1
identity.

The recommended pattern is:

1. `imports_trello_preview` against a fresh
   workspace to confirm the shape.
2. `imports_trello_apply` once.
3. If the user wants a second copy (e.g. for
   a sandbox), create a new target workspace
   and re-apply.

A future v1.3.0 PR may add an optional
`idempotencyKey` form field; the plumbing
exists (`IImportService.ImportTrelloJsonAsync`
can grow a `string? idempotencyKey = null`
parameter without breaking the public shape).

## 8. Limitations

- **No attachments.** Trello exports do not
  include the actual file bytes for attached
  files, so attachments are skipped. Re-upload
  them through the Web UI after the import.
- **No comments.** Trello comment threads are
  not part of the JSON export.
- **No custom fields.** Trello Power-Up data
  is not in the JSON export.
- **No recurring cards.** Cardscape's
  `Recurrence` block is not populated from
  the import.
- **No board extensions / power-ups.** The
  imported boards have no calendar feed,
  custom dashboard, or board extension data.
- **Single target workspace.** Every board
  in the import lands in the same target
  workspace. The "import into a fresh
  workspace" auto-create mode the v1.1.0
  plan listed is not implemented; the
  caller must create the workspace first.

## 9. Verification (end-to-end)

1. Export a Trello workspace as JSON.
2. Open `Workspaces / <id> / Import` in the
   Web UI.
3. Drop the file, hit "Preview import". The
   panel should show the board / list / card
   counts and a few sample names.
4. Hit "Apply import". The target workspace
   should now contain the boards, lists,
   cards, labels, and (email-matched) members.
5. From the MCP client: call
   `imports_trello_preview` against the same
   file. The response should mirror the
   preview panel.
6. From a curl one-liner:
   ```bash
   curl -X POST https://localhost:5001/api/imports/trello \
     -H "Authorization: Bearer $TOKEN" \
     -F "file=@boards.json" \
     -F "targetWorkspaceId=$WS_ID" \
     -F "previewOnly=true"
   ```
   The response should be a 200 with the
   same `ImportResult` shape and
   `preview.wasApplied = false`.

## 10. References

- [`01-build-your-own-mcp-client.md`](01-build-your-own-mcp-client.md) —
  the general MCP client recipe (covers
  the `imports_trello_*` tools by name).
- [`../audits/2026-07-30/07-polish.md` §5.6](../audits/2026-07-30/07-polish.md) —
  the audit that flagged the missing
  preview / dry-run path. The
  `previewOnly` form field resolves the
  audit's PARTIAL verdict.
- [`../api/02-openapi-spec.md`](../api/02-openapi-spec.md) —
  the OpenAPI definition for
  `POST /api/imports/trello`.
