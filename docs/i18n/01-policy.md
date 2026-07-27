# i18n policy

> Which languages Cardscape ships in, which artifacts get
> translated, who translates, and how translation is
> governed. The **how** (the workflow, the file layout,
> the review process) is in
> [`02-translation-workflow.md`](02-translation-workflow.md).
> This file is the **what** and the **who**.

---

## 1. The supported languages

Cardscape ships in **English** (the source of truth) and
**Spanish** (the maintainer's fluent second language).
Other languages are added by community contribution.

| Code | Language | Status | Translator | Reviewer |
|---|---|---|---|---|
| `en` | English | source of truth | the maintainer | the maintainer |
| `es` | Spanish (Castilian / Argentine) | first-class | the maintainer | the maintainer |
| _other_ | _community-contributed_ | reviewed | the contributor | the maintainer |

The supported-language list is **not** a "we will reject
your PR if you add a language we don't support". It is a
"we will maintain these ourselves; other languages are
welcome and we will help you land them".

---

## 2. What gets translated

| Artifact | Translated? | Why |
|---|---|---|
| **UI strings** (the Blazor components' visible text) | yes | the user sees them |
| **Error messages** (`ProblemDetails.Detail`, the MCP tool's `content`) | yes (in a future i18n pass) | the user sees them |
| **The website** (`site/index.html`, `site/README.md`) | yes | the public-facing copy |
| **The README** (`README.md`) | yes | the first thing a visitor reads |
| **The community files** (`CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`) | yes | the entry point for contributors |
| **The `docs/` set** (`AGENTS.md`, `roadmap/`, `architecture/`, `development/`, `design/`, `security/`, `i18n/`, `brand/`, `positioning/`, `ai/`, `operations/`) | yes (in a future i18n pass) | the maintainer reference |
| **Code identifiers** (class names, method names, variable names) | no | the language does not change the code |
| **Log messages** | no | logs are for the operator, not the user; the operator speaks English |
| **Git commit messages** | no | the commit history is in English by convention |
| **The ADRs** (`docs/adr/`) | no | the ADRs are an internal record; English is fine |
| **The `LICENSE`** | no | the license is the canonical English text |
| **Brand names** (Cardscape, MCP, .NET, etc.) | no | brand names are not translated |

The rule of thumb: **translate anything a user or a
contributor reads; do not translate anything an operator
or a maintainer reads**.

---

## 3. The tone of the translation

The English text is written in the project's voice (see
[`../roadmap/02-product-positioning.md`](../roadmap/02-product-positioning.md)
§6 "Voice and tone"). The translation preserves the voice:

- **Direct.** No hedging, no "we hope", no "we believe".
- **Specific.** Numbers, versions, file paths, commit hashes.
- **Confident without being arrogant.**
- **Calm.** No exclamation points, no marketing hype.
- **First person plural, sparingly.**

A translation that diverges from the voice is a review
failure. The reviewer asks for a revision.

---

## 4. The translation is per-language, not per-region

Cardscape ships **one** Spanish translation (`es`), not
multiple regional variants (`es-AR`, `es-MX`, `es-ES`).
The maintainer is fluent in Argentine Spanish and writes
the `es` translation in that variety; the translation is
acceptable for all Spanish readers, with the understanding
that some regional expressions may be unfamiliar.

If a regional variant becomes necessary (e.g. the project
gains a community in Spain and a contributor wants to
differentiate `es-ES`), the variant is added as a separate
language with its own translator. The base `es` is the
fallback.

---

## 5. The translation review

Every translation PR is reviewed by the maintainer. The
review covers:

1. **Accuracy.** The translation says the same thing as
   the source.
2. **Voice.** The translation matches the project's voice.
3. **Terminology.** The translation uses the same terms
   across files (a "card" is always a "card", not sometimes
   a "ticket"; a "workspace" is always a "workspace", not
   sometimes a "project").
4. **Formatting.** Markdown is preserved; code samples are
   not translated; links are not translated.
5. **No machine translation without review.** A PR that is
   a raw machine translation (Google Translate, DeepL,
   etc.) is rejected with a request to review by a human.

The maintainer is the final reviewer for English (the
source of truth is the maintainer's English) and for
Spanish (the maintainer's fluent second language). For
other languages, the reviewer is the maintainer plus a
fluent speaker of the target language.

---

## 6. The translation is part of the release

A change to the English source that is not translated is
**acceptable** for an internal artifact (the ADRs, the
`design/` set) but is **not acceptable** for a user-facing
artifact (the website, the community files). The PR that
changes the English source is **blocked** until the
translation is added (or a maintainer accepts the lag
with a written justification).

This rule prevents the project from drifting: a feature
that ships in English but not in Spanish is, in practice,
an English-only feature for a Spanish-speaking user.

---

## 7. What the project does not do

- **Locale-specific number formats in the API.** The API
  returns ISO 8601 dates, ISO 4217 currency codes, and SI
  units. The client formats for the user's locale. The
  server does not.
- **Right-to-left layout.** The UI does not support RTL
  today. When it does, the work is a separate effort (see
  [`../design/04-accessibility.md`](../design/04-accessibility.md)).
- **Machine translation of the docs at runtime.** A
  "translate this page" button is **not** a feature. The
  translation is committed to the repo, not generated on
  the fly. This is a deliberate choice: a generated
  translation is not a translation, and the project does
  not ship unmaintained text.
- **Pluralization rules per language.** The English
  text uses the simple `singular / plural` form. When a
  language with more plural forms (Russian, Arabic,
  Polish) is added, the i18n framework (ResourceManager,
  ICU) is used to handle the forms.

---

## 8. When to revisit

This document is revisited when:

1. A third language is added to the supported list.
2. A new artifact type needs translation (e.g. the
   MCP server's prompts).
3. The translation review process changes (e.g. a
   per-language co-maintainer).
4. The voice and tone of the English text changes
   (the translation must be updated to match).

Until then, this document is the source of truth for the
i18n policy in Cardscape.
