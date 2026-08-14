# 02 — Importing Cardscape Kanban JSON

> How to import Cardscape's vendor-neutral Kanban JSON
> end-to-end. Covers the JSON shape, the
> explicit `POST /api/imports/kanban/preview` and
> `/apply` endpoints, the MCP tools, and the mapping rules.

---

## 1. The short version

Cardscape defines a vendor-neutral `boards.json` interchange
format. The importer reads that format and creates a matching set of
boards / lists / cards / labels under a target Cardscape workspace of the
caller's choice.

Three ways to drive the import:

1. **Web UI** — `Workspaces / <id> / Import`. File
   picker, submit, see the live preview, submit
   again to apply.
2. **REST API** — explicit multipart endpoints
   `POST /api/imports/kanban/preview` and
   `POST /api/imports/kanban/apply`.
3. **MCP** — `imports_kanban_preview` and
   `imports_kanban_apply` tools. The preview
   tool is a dry-run; the apply tool writes to
   the database.

## 2. JSON shape

Cardscape Kanban JSON is an array of board objects. Cardscape reads the fields
it needs; unknown fields are silently dropped.

```json
[
  {
    "id": "5d9...e8",
    "name": "Personal board",
    "description": "The board description",
    "lists": [
      { "id": "5da...01", "name": "Todo", "closed": false },
      { "id": "5da...02", "name": "Doing", "closed": false },
      { "id": "5da...03", "name": "Done",  "closed": true  }
    ],
    "cards": [
      {
        "id": "5db...c1",
        "name": "First card",
        "description": "Body of the first card",
        "listId": "5da...01",
        "closed": false,
        "dueDate": "2026-09-01T12:00:00.000Z",
        "labelIds": ["5dc...01", "5dc...02"],
        "memberIds": ["5dd...01", "5dd...02"]
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

The array may contain more than one board; each board becomes one Cardscape board inside
the target workspace.

## 3. REST endpoint

```
POST /api/imports/kanban/preview
POST /api/imports/kanban/apply
Content-Type: multipart/form-data; boundary=...

(file=<boards.json>; targetWorkspaceId=<guid>)
```

- `file` — the `boards.json` upload. Required.
  The endpoint reads the stream into
  `IImportService.ImportKanbanJsonAsync`. Max
  size is 10 MB at both endpoint and service boundaries.
- `targetWorkspaceId` — the Cardscape workspace
  the import should land in. Required. The
  caller must be a member of the target workspace.

The route selects the operation. `/preview` never writes;
`/apply` writes. The removed unsuffixed route does not default
to either behavior.

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

The `/preview` route (or the `imports_kanban_preview`
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

The Cardscape interchange format is intentionally small.
Unknown JSON fields are ignored, and unsupported Cardscape
features remain empty in imported data.

| JSON field | Cardscape | Notes |
|---|---|---|
| `boards[]` | one `Board` per element | All boards land in the **same** target workspace. |
| `boards[].name` | `Board.Name` | Required. Empty / whitespace names abort the import with `imports.invalid_board_name`. |
| `boards[].description` | `Board.Description` | Optional plain text. |
| `boards[].lists[]` | one `BoardList` per element | Array order determines position. |
| `lists[].name` | `BoardList.Name` | Required. Empty / whitespace names abort the import. |
| list array index | `BoardList.Position` | Normalized in increments of 1024. |
| `cards[]` | one `Card` per element | Cards whose `listId` is unknown are skipped. |
| `cards[].name` | `Card.Title` | Required. Empty / whitespace names abort the import. |
| `cards[].description` | `Card.Description` | Optional plain text. |
| `cards[].dueDate` | `Card.DueDate` | Optional ISO 8601 value; invalid values are ignored. |
| `cards[].listId` | card ↔ list join | Must match `lists[].id` in the same board. |
| `cards[].labelIds[]` | card ↔ label join | Unknown label ids are ignored. |
| `boards[].labels[]` | one `Label` per element | A blank name falls back to its color; blank name and color entries are skipped. |
| `boards[].members[]` | preview count only | Accounts are never created or linked by import. |

## 6. MCP tools

The MCP server exposes two tools that wrap the
endpoint:

### `imports_kanban_preview`

| | |
|---|---|
| Inputs | `boardsJson` (string), `targetWorkspaceId` (GUID). |
| Outputs | The `ImportResult` JSON above, with `wasApplied = false`. |
| Notes | Pure dry-run. The database is never touched. The MCP host reads the file (not the API server) and dispatches the call as `previewOnly=true`. |

### `imports_kanban_apply`

| | |
|---|---|
| Inputs | `boardsJsonPath` (string), `targetWorkspaceId` (GUID). |
| Outputs | The `ImportResult` JSON above, with `wasApplied = true` and populated `importedXxxIds`. |
| Notes | Calls the same pipeline with `previewOnly=false`. The MCP client's API token must be authorized on the target workspace. |

The two tools share the same service pipeline and choose the
mode explicitly. REST uses distinct routes so an omitted form
field can never turn a preview into a write.

## 7. Idempotency

Imports are **not** idempotent. Running the
same `boards.json` twice against the same
target workspace will create a duplicate
board / list / card tree. The service intentionally does not
deduplicate source ids; each apply creates a new Cardscape
aggregate tree.

The recommended pattern is:

1. `imports_kanban_preview` against a fresh
   workspace to confirm the shape.
2. `imports_kanban_apply` once.
3. If the user wants a second copy (e.g. for
   a sandbox), create a new target workspace
   and re-apply.

Idempotency is not currently part of this import contract.

## 8. Limitations

- **No attachments.** The interchange format does not include
  file bytes. Re-upload
  them through the Web UI after the import.
- **No comments.** Comment threads are not part of the format.
- **No custom fields.** Custom field data is not part of the format.
- **No recurring cards.** Cardscape's
  `Recurrence` block is not populated from
  the import.
- **No board extensions.** The
  imported boards have no calendar feed,
  custom dashboard, or board extension data.
- **Single target workspace.** Every board
  in the import lands in the same target
  workspace. The "import into a fresh
  workspace" auto-create mode the v1.1.0
  plan listed is not implemented; the
  caller must create the workspace first.

## 9. Verification (end-to-end)

1. Produce a JSON file matching the schema above.
2. Open `Workspaces / <id> / Import` in the
   Web UI.
3. Drop the file, hit "Preview import". The
   panel should show the board / list / card
   counts and a few sample names.
4. Hit "Apply import". The target workspace
   should now contain the boards, lists, cards and labels.
   Kanban members are counted in the summary but are not mapped
   to Cardscape accounts.
5. From the MCP client: call
   `imports_kanban_preview` against the same
   file. The response should mirror the
   preview panel.
6. From a curl one-liner:
   ```bash
   curl -X POST https://localhost:5001/api/imports/kanban/preview \
     -H "Authorization: Bearer $TOKEN" \
     -F "file=@boards.json" \
     -F "targetWorkspaceId=$WS_ID"
   ```
   The response should be a 200 with the
   same `ImportResult` shape and
   `preview.wasApplied = false`.

## 10. References

- [`01-build-your-own-mcp-client.md`](01-build-your-own-mcp-client.md) —
  the general MCP client recipe (covers
  the `imports_kanban_*` tools by name).
- [`../audits/2026-07-30/07-polish.md` §5.6](../audits/2026-07-30/07-polish.md) —
  the audit that flagged the missing
  preview / dry-run path. The
  the explicit preview action resolves the audit's PARTIAL verdict.
- [`../api/02-openapi-spec.md`](../api/02-openapi-spec.md) —
  the OpenAPI definition for
  Kanban import endpoints.
