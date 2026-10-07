# ADR 0014 — Enum-typed response DTOs

Date: 2026-10-07
Status: Accepted. Breaking change to the v1.2.0 HTTP and MCP response contract.

## Context

[`docs/api/00-conventions.md`](../api/00-conventions.md#wire-enums) says enum
values are camelCase names on the wire and numeric CLR values are rejected. The
API host enforces that with `JsonStringEnumConverter(CamelCase,
allowIntegerValues: false)`, but the converter only applies to enum-typed
members. Five Application DTO members cast their domain enum to `int`, so they
leaked the numeric value:

| DTO | Member | Domain enum |
| --- | --- | --- |
| `BoardExtensionDto` | `kind` | `ExtensionKind` |
| `CustomFieldDefinitionDto` | `kind` | `CustomFieldKind` |
| `CustomFieldValueDto` | `kind` | `CustomFieldKind` |
| `BoardAutomationRuleDto` | `trigger` | `AutomationTrigger` |
| `BoardAutomationRuleDto` | `action` | `AutomationAction` |

Requests to the same endpoints already took the camelCase name, so a client had
to write `"customFields"` and read back `0`. The Web client, which reads enums
as names only, failed to deserialise these responses and the board page
rendered blank; it was patched with per-property `JsonNumberEnumConverter`s.

## Decision

The five members are declared with their domain enum type and serialise as
camelCase names, like every other enum in the API. There is no exception to the
wire-enum rule. The Web client's numeric converters are removed.

`WireEnumContractTests` (architecture tests) fails when an Application DTO
exposes a member as a non-enum while the Web DTO of the same name reads it as
an enum, and when the Application enum has a member the Web enum cannot
deserialise. `OpenApi_EnumBackedResponseFields_Are_CamelCase_String_Enums`
(integration tests) pins the published schemas.

## Consequences

- **Breaking:** clients that read `kind`, `trigger` or `action` from these
  responses as integers must read the camelCase name (`"customFields"`,
  `"text"`, `"cardMoved"`, `"moveCardToList"`, …). The .NET SDK does not model
  these endpoints and is unaffected.
- The MCP tools `boards_*_extension*` and `automation_*` return the same DTOs,
  so these members are now enum-typed in tool results as well; the MCP SDK's
  serializer options (not the API host's) decide how they are rendered. Their
  numeric *arguments* are unchanged. Per `docs/AGENTS.md` rule 9 this is an
  MCP return-type change; the MCP server ships under the repository-wide
  version, so the next release must carry the major bump.
- `DisableBoardExtensionCommand` and `UpdateBoardExtensionConfigCommand` still
  take the kind as `int`; they are internal messages, not wire contracts.
