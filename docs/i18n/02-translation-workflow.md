# Translation workflow

> The **how** of translating Cardscape's user-facing
> artifacts. The **what** (which languages, which
> artifacts) and the **who** (who translates, who
> reviews) are in [`01-policy.md`](01-policy.md). This
> file is the workflow: the file layout, the PR process,
> the review checklist, the tooling.

---

## 1. The file layout: sibling files

The simplest, lowest-tooling layout: every translatable
file has a sibling file with the language code as a
suffix.

```
README.md
README.es.md
CONTRIBUTING.md
CONTRIBUTING.es.md
CODE_OF_CONDUCT.md
CODE_OF_CONDUCT.es.md
docs/roadmap/02-product-positioning.md
docs/roadmap/02-product-positioning.es.md
site/index.html
site/index.es.html
...
```

The naming convention is **`<basename>.<lang>.<ext>`**. The
language code is a **BCP 47** subtag (the same codes used
in the `<html lang="...">` attribute and in the
`Accept-Language` HTTP header).

The base file (no suffix) is the **source of truth**, in
English. The translation PR adds the sibling file.

For the **website** (`site/index.html` → `site/index.es.html`),
the deployment script picks the right file based on the
`Accept-Language` header. Until the deployment script is
in place, the site is English-only; the `index.es.html` is
written but not deployed.

---

## 2. The PR process

A translation PR follows the same flow as any other PR
(see [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md)). The
specifics:

1. **The PR description names the language.** "Add Spanish
   translation of `README.md`".
2. **The PR diff contains only the new file** (or a
   minimal set of new files). The PR does not change the
   English source; the source is the maintainer's job.
3. **The PR is labeled `i18n` and the language.** The
   maintainer adds the label during review if missing.
4. **The PR is reviewed by the maintainer.** For languages
   other than English and Spanish, the maintainer pulls
   in a fluent speaker of the target language.
5. **The PR is squashed and merged.** The commit message
   is `i18n(<lang>): translate <file>` (e.g. `i18n(es):
   translate README.md`).

A PR that mixes a translation with a code change is
**rejected** in review. The translation goes in one PR; the
code change goes in another.

---

## 3. The translation checklist

The reviewer runs the checklist from
[`01-policy.md`](01-policy.md) §5 on every translation PR:

1. **Accuracy.** The translation says the same thing as
   the source.
2. **Voice.** The translation matches the project's voice.
3. **Terminology.** The translation uses the same terms
   across files.
4. **Formatting.** Markdown is preserved; code samples are
   not translated; links are not translated.
5. **No machine translation without review.** A PR that is
   a raw machine translation is rejected.

The reviewer also checks:

6. **The file's path follows the convention.** `<basename>.<lang>.<ext>`.
7. **The language code is BCP 47.** `es`, not `spa` or
   `es-ES` (unless a regional variant is added; see
   `01-policy.md` §4).
8. **The file is in the same folder as the source.** Not
   in a `locales/` subfolder; the sibling convention.

---

## 4. The translation is a separate concern from the source

When the English source is updated, the translation **is
not** automatically updated. The translator (or a
contributor) re-reads the source, identifies the changes,
and updates the translation. The PR is a new translation
PR, not an edit to the existing one.

This is intentional. An auto-update would miss the
**context** of the change (e.g. "we changed the wording
here because X; the translation should reflect X, not just
the new words"). The translator is a human who reads both
the old and the new source.

A drift detector (added in Phase 5) flags translations
that are out of sync with the source. The drift detector
does not auto-update; it pings the translator.

---

## 5. The translation is per-file, not per-phrase

Cardscape does not use a per-phrase i18n framework (no
`.po` files, no JSON locale files, no Crowdin, no Weblate)
in Phase 1. The trade-off:

- **Pro**: zero tooling, zero dependencies, every
  translation is a plain Markdown file that any contributor
  can edit.
- **Con**: a phrase that appears in five files must be
  translated five times. A change to the phrase must be
  made in five files.

The trade-off is acceptable today because the project's
surface is small. When the surface grows past ~30
translatable files, a per-phrase framework is added. The
candidate is **Crowdin** (hosted, free for open-source) or
**Weblate** (self-hosted, .NET-friendly).

---

## 6. The terminology glossary

Some terms are project-specific and must be translated
the same way across files. The glossary is the source of
truth.

| English | Spanish | Notes |
|---|---|---|
| Card | Tarjeta | the atomic unit of a board; **not** "ficha" |
| List | Lista | a column on a board; **not** "columna" |
| Board | Tablero | the kanban surface; **not** "panel" |
| Workspace | Espacio de trabajo | the top-level container; **not** "área de trabajo" |
| Member | Miembro | a user in a workspace; **not** "usuario" |
| Label | Etiqueta | a color-coded tag; **not** "rótulo" |
| Comment | Comentario | a note on a card; **not** "observación" |
| Attachment | Adjunto | a file on a card; **not** "archivo adjunto" |
| Due date | Fecha de vencimiento | the card's due date; **not** "fecha límite" |
| Checkbox | Casilla de verificación | a checklist item; **not** "casilla" |
| MCP server | Servidor MCP | the Model Context Protocol server; "MCP" is not translated |
| Cardscape | Cardscape | the project name; **not** translated |

The glossary is the maintainer's responsibility. A
contributor who proposes a new term is asked to update the
glossary in the same PR.

---

## 7. The code identifiers are not translated

Code identifiers (class names, method names, variable
names, NuGet package names, project names) are not
translated. The English identifier is used in every
language.

The exception is **user-facing strings inside the code**
(e.g. a `nameof(Workspace)` for display, an error message
template, a button label). Those strings are extracted
into resource files in a future i18n pass and translated
per language.

The extraction is added in Phase 1 (or later, when the UI
has enough strings to make the extraction worth the
tooling). Until then, the strings are in the code, in
English, and the language is fixed.

---

## 8. The translation is in the same git history

The translation file is committed to the same git history
as the source. The translation is not in a separate
repository, not in a separate branch, not in a separate
git submodule. The history is the history.

This means a `git log -- README.md` shows the source's
history; a `git log -- README.es.md` shows the
translation's history; the two are correlated by the
`i18n(<lang>)` prefix on the translation commits.

---

## 9. The tooling

Today, the tooling is **none**. The translation is a
plain text file. The diff is a plain `git diff`. The
review is a human review.

A future PR (Phase 5) may add:

- **A drift detector** that compares the source and the
  translation and flags out-of-sync sections.
- **A glossary checker** that flags translations that use
  a non-glossary term.
- **A spell checker** for the target language.
- **A link checker** that ensures the relative links in
  the translation still point to the right files.

None of these are required for the project to function.
They are quality-of-life improvements.

---

## 10. Anti-patterns (do not do this)

- **A machine translation without a human review.** The
  translation is wrong more often than not, and the wrong
  terms become entrenched.
- **A translation that diverges from the source.** If the
  source is updated, the translation is updated in the
  same release. A translation that is "behind" the source
  is a bug.
- **A translation that uses a different voice.** The voice
  is part of the project. A translation that is more
  casual, or more formal, or more "marketing" is a
  different project.
- **A translation that translates the brand names.**
  "Cardscape" is "Cardscape" in every language.
- **A translation that translates the code identifiers.**
  The identifiers are the same in every language.
- **A per-phrase i18n framework added too early.** The
  framework has a cost (tooling, complexity, contributor
  onboarding). The cost is justified when the surface
  grows; not before.

---

## 11. When to revisit

This document is revisited when:

1. A per-phrase i18n framework is added.
2. A drift detector is added.
3. A new language is added.
4. A new artifact type is translated (e.g. the MCP
   server's prompts).

Until then, this document is the source of truth for the
translation workflow in Cardscape.
