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
into `.resx` resource files in `src/Cardscape.Web/Resources/`
and translated per language. The practical extraction
workflow — how to add a key, how to add a language, the
placeholder convention — is in §11.

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

## 11. Practical .resx extraction guide (Blazor UI strings)

Sections 1–10 describe the file-sibling convention used for
**docs** (Markdown) and the **website** (HTML). The Blazor UI
strings follow a different workflow: they live in **`.resx`**
resource files under
`src/Cardscape.Web/Resources/`, consumed by
`IStringLocalizer<SharedResource>`.

This section is the **practical extraction guide** the
execution plan §5.2 asked for: how to add a key, how to add a
language, how to keep the keys in sync.

### 11.1 The layout

```
src/Cardscape.Web/
├── Resources/
│   ├── SharedResource.cs           # marker class (empty)
│   ├── SharedResource.resx         # English (source of truth)
│   ├── SharedResource.es.resx      # Spanish
│   ├── SharedResource.<lang>.resx  # one file per language
│   └── test.txt                    # placeholder so the folder
│                                   # ships in the .csproj
```

`SharedResource.cs` is a **marker class**. It has no members.
The localization system uses the class name to locate the
matching `SharedResource.<culture>.resx` files. The class
itself contains no strings.

The `.resx` files are auto-discovered: the
`AddLocalization(options => { options.ResourcesPath = "Resources"; })`
call in `src/Cardscape.Web/Program.cs` configures the path,
and the framework reads `SharedResource.<culture>.resx` for
every culture for which a `.resx` file is shipped under
`Resources/`. The supported-culture set is therefore
**implicit in the `.resx` file set** — there is no separate
"supported cultures" configuration in DI on the WASM
client (see §12 for why).

### 11.2 Adding a new key (the English source)

1. Open `src/Cardscape.Web/Resources/SharedResource.resx`.
2. Add a `<data>` entry. The convention is **`<Scope>_<CamelCase>`**,
   e.g. `Boards_CreateNew`, `Login_EmailLabel`,
   `Settings_GoogleCalendar_Title`. The scope is the page or
   area; the rest is a camel-case description of the string.
3. The `<value>` is the English text. The `<comment>` is
   optional context for the translator (e.g. "button on the
   board card", "tooltip on the trash icon").
4. Use the key in a Razor component via
   `@inject IStringLocalizer<SharedResource> L` and
   `@L["Boards_CreateNew"]`.

A PR that adds a new key to `SharedResource.resx` (the
English source) **must** also add the key to every
`SharedResource.<lang>.resx` file that exists, even if the
translation is a placeholder. A missing key falls back to
the English value, but that breaks the "the translation file
is complete" invariant the reviewer relies on. See §11.4 for
the placeholder convention.

### 11.3 Adding a new language

1. Pick a **BCP 47** subtag (see `01-policy.md` §1 and §4
   for the supported-language and per-language-not-per-region
   rules).
2. Copy `src/Cardscape.Web/Resources/SharedResource.es.resx`
   to `src/Cardscape.Web/Resources/SharedResource.<lang>.resx`.
3. Translate every `<value>`. Keys missing from the new file
   fall back to the English source automatically.
4. In `src/Cardscape.Web/Program.cs`:
   - Add the language code to the
     `string[] supportedCultures = { ... }` array. This is
     the documentation list of the supported set (see §12
     for why the plan's named API cannot be applied on
     WASM) and the source a future `CulturePicker` reads
     from.
5. Update `01-policy.md` §1 (the supported-languages table) and
   §6 (the glossary) if the language has terminology
   differences from English.
6. Add the language code to the relevant `.csproj` `<ItemGroup>`
   so the `.resx` file is **embedded as a resource** (see
   `SharedResource.es.resx` in `Cardscape.Web.csproj` for the
   pattern; the default Blazor `.csproj` includes `**\*.resx`
   in the project, which auto-embeds the file).

The review follows the same rules as a Markdown translation
(§2 and §3): the diff is **only** the new `.resx` file plus
the one-line `Program.cs` update (the
`string[] supportedCultures` array); the English source is
not touched.

### 11.4 Placeholder convention (when the translation lags)

A key in `SharedResource.<lang>.resx` whose translation is
not yet done uses the **placeholder**:

```xml
<value>__TODO_es__ Boards_CreateNew</value>
```

The `__TODO_<lang>__` prefix makes the lag **visible** in the
UI (a Spanish user sees "__TODO_es__ Boards_CreateNew" until
the translator lands the PR) instead of silently falling back
to the English value. The reviewer rejects a PR that leaves a
non-prefixed English value in a non-English `.resx` file —
the placeholder is the explicit signal that the translation
is in progress.

The drift detector (added in Phase 5, per §9) flags every
`__TODO_<lang>__` placeholder as a pinging-the-translator
item, not as a bug.

### 11.5 The key naming rules

- **Scope first.** `Boards_CreateNew`, not `CreateNewBoard`.
  The scope is the page or feature area; it groups related
  keys in the `.resx` editor and in the search.
- **Camel case, not snake case.** `_` is the scope separator;
  the rest is camel case. `Login_EmailLabel`, not
  `Login_Email_Label` or `login.emaillabel`.
- **No `Page_` prefix.** The scope is the page name
  (`Boards`, `Login`, `Settings`), not the file name.
- **No duplicates.** A key exists in exactly one place. The
  build warns on duplicate keys; a duplicate is rejected in
  review.
- **The terminology glossary is the source of truth.** A key
  that contains a glossary term (`Card`, `List`, `Board`,
  `Workspace`, `Member`, `Label`, `Comment`, `Attachment`,
  `Due date`, `Checkbox`, `MCP server`) uses the term in the
  **English source**; the translator uses the
  glossary-mandated translation. See `01-policy.md` §6
  (Glossary) and the `## 6. The terminology glossary` table
  in this file for the canonical mappings.

### 11.6 Verifying the extraction

After adding a key, the build must stay green and the
resource must be reachable:

1. `dotnet build src/Cardscape.Web/Cardscape.Web.csproj` —
   0 errors, 0 warnings. The `.resx` compiler emits a
   duplicate-key warning if a key is added twice; that is the
   first-line check.
2. The Razor component uses
   `@inject IStringLocalizer<SharedResource> L` and renders
   `@L["Boards_CreateNew"]` — the value comes from the
   `.resx` for the current `CultureInfo`.
3. To verify a translation, temporarily set
   `Culture:Default` in `wwwroot/appsettings.json` to the
   target language code, run the app, and confirm the UI
   renders the translated value (no `__TODO_<lang>__`
   prefix).

### 11.7 What is NOT in the `.resx`

- **Log messages.** Logs are for the operator, not the user;
  the operator speaks English. See `01-policy.md` §2.
- **Code identifiers.** Class names, method names, variable
  names, NuGet package names, project names. See
  `01-policy.md` §2 and the `## 7. The code identifiers are
  not translated` section above.
- **API error messages.** `ProblemDetails.Detail` and the MCP
  tool `content` are not yet translated. They are a future
  i18n pass; see `01-policy.md` §2.
- **ADRs, license, brand names.** See `01-policy.md` §2.

---

## 12. Blazor WebAssembly culture-resolution caveat

The plan called for `app.UseRequestLocalization(...)` to read
the current culture from the `Accept-Language` request header
on the server. That **cannot work on Blazor WebAssembly**:

- `UseRequestLocalization` is **server-side middleware** that
  runs in the ASP.NET Core request pipeline. It reads the
  `Accept-Language` header on the **server** and sets
  `CultureInfo.CurrentCulture` for that request.
- Blazor WebAssembly is a **client-side single-page app**:
  the server serves `index.html`, the `.wasm` payload, and
  the `.dll` assemblies as static files. There is no per-
  request server pipeline, so `UseRequestLocalization` has
  nothing to run on.
- The `Accept-Language` header the browser sends to fetch
  the static assets is a **fetch-time** header; it is not
  visible to the running WASM code. The browser's preferred
  language is exposed to JavaScript and .NET-on-WASM as
  `navigator.language` (and `navigator.languages` for the
  full preference list), not as the `Accept-Language`
  request header.

The configuration in `src/Cardscape.Web/Program.cs` reflects
this:

```csharp
// Program.cs: (the localization block)
const string defaultCulture = "en";
string[] supportedCultures = { "en", "es" };

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

string? configuredDefault = builder.Configuration["Culture:Default"];
CultureInfo defaultCultureInfo = string.IsNullOrWhiteSpace(configuredDefault)
    ? new CultureInfo(defaultCulture)
    : new CultureInfo(configuredDefault);
CultureInfo.DefaultThreadCurrentCulture = defaultCultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = defaultCultureInfo;
```

- The `AddLocalization` call registers the `.resx` resource
  path so `IStringLocalizer<SharedResource>` resolves
  `SharedResource.<culture>.resx` for the current culture.
- The supported-culture set is **implicit in the set of
  `SharedResource.<culture>.resx` files** shipped under
  `Resources/` — today `en` (default) + `es`. The
  `string[] supportedCultures = { "en", "es" }` array is
  the **documentation** list of the supported set; a future
  `CulturePicker` reads from it. There is no DI-registered
  "supported cultures" object on the WASM client (see the
  note below on why the plan's literal named API does not
  apply).
- The plan's literal `AddLocalization(opts => opts.SetDefaultCulture
  ("en").AddSupportedCultures("en", "es"))` shape is a
  **server-side API**: the `SetDefaultCulture` /
  `AddSupportedCultures` extension methods live on
  `RequestLocalizationOptions` (the type the
  `UseRequestLocalization` middleware reads), not on
  `LocalizationOptions` (the `AddLocalization` callback's
  options type). On the server, the type is in the
  `Microsoft.AspNetCore.App` shared framework. On Blazor
  WebAssembly the `Microsoft.NET.Sdk.BlazorWebAssembly` SDK
  only references a subset of the framework, and adding
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
  fails with `NETSDK1082` (no `browser-wasm` runtime pack).
  The standalone `Microsoft.AspNetCore.Localization` NuGet
  package tops out at 2.3.11 (ASP.NET Core 2.x era) and is
  not compatible with the .NET 10 SDK. There is no
  clean way to surface the plan's named API on the WASM
  client in this SDK. The configuration the plan asked for
  (default culture + supported culture set) is preserved
  via the constants and the implicit `.resx` set.
- The current culture is set via
  `CultureInfo.DefaultThreadCurrentCulture` /
  `DefaultThreadCurrentUICulture` (with the default taken
  from `Culture:Default` in `wwwroot/appsettings.json`, or
  `"en"` if the setting is missing).

### 12.1 There is no `CulturePicker` (yet)

A previous version of the localization comment in
`Program.cs` mentioned a `CulturePicker` that stores the
choice in `localStorage`. **That class does not exist in the
codebase today** (verified by `grep` on
`src/Cardscape.Web/`). The localization works because
`IStringLocalizer<SharedResource>` resolves the right
`.resx` for whatever culture is current — but the culture
never changes after startup.

A future PR can add a real `CulturePicker` in one of two
shapes:

1. **`navigator.language` on startup.** A small piece of
   startup code in `Program.cs` reads
   `navigator.language`, falls back to the closest supported
   culture in the `string[] supportedCultures` array
   (or to `defaultCulture` if none match), and sets
   `CultureInfo.DefaultThreadCurrentCulture` accordingly.
   No persistence — the user gets the browser's preferred
   language on every load.
2. **A `CulturePicker` component + `localStorage` override.**
   A `<CulturePicker>` dropdown in the user menu writes the
   chosen culture to `localStorage`; the startup code reads
   `localStorage` first, falls back to `navigator.language`,
   falls back to `defaultCulture`. This is the more
   user-friendly option and is the one the original comment
   described.

The configuration in `Program.cs` is shaped to support both:
the supported-culture array is the single source of truth
that the picker reads from and that the startup fallback
matches against.

### 12.2 The "current culture" today

Until the `CulturePicker` lands, the practical effect is:

- A user who has not changed any setting gets the
  `defaultCulture` (`"en"`, or the value of
  `Culture:Default` in `wwwroot/appsettings.json`).
- The `IStringLocalizer<SharedResource>` resolves the
  English `.resx` (or the configured default), so the UI is
  in English.
- Changing the culture at runtime (e.g. by setting
  `CultureInfo.DefaultThreadCurrentCulture` from the
  browser dev tools) **does** swap the UI to the new
  culture — the localizer re-resolves on the next render.
  The change does not persist across reloads.

The localization pipeline is **correct** for the
default-culture case (137 EN + 140 ES keys ship; see
`01-policy.md` §1 and the audit at
[`../audits/2026-07-30/07-polish.md`](../audits/2026-07-30/07-polish.md)
§5.1). The missing piece is the runtime culture switch, which
is a separate, follow-up item — not a blocker for the
default-culture experience.

---

## 13. Client-side culture resolution in Blazor WebAssembly (D7 — v1.2.0, G12 follow-up)

The v1.1.0 G12 push was the "There is no `CulturePicker`
(yet)" item above. The v1.2.0 workstream (D7) shipped
the picker. The implementation:

- **Service**: `Cardscape.Web.Services.CultureSwitcher`
  (singleton) owns the current culture, the per-culture
  in-memory dictionary, and the `localStorage`
  persistence under the key `Cardscape.Culture`.
- **Localizer**: `HttpBackedStringLocalizer` is
  registered as the `IStringLocalizer<SharedResource>`
  implementation in `Program.cs`. It reads from the
  picker when the key is present and falls back to the
  embedded English `StringLocalizer<SharedResource>`
  otherwise.
- **UI**: `Shared/LanguageSwitcher.razor` is a
  `RadzenDropDown` rendered in both the
  `<Authorized>` and `<NotAuthorized>` branches of
  `Layout/MainLayout.razor`. On change it calls
  `CultureSwitcher.SetCultureAsync(culture)`; the
  picker persists the choice, fetches the matching
  `SharedResource.{culture}.resx` static web asset,
  parses the `<data name=…>` entries into the
  dictionary, and raises a `Changed` event that the
  layout converts into a `StateHasChanged()`.
- **Wiring**: `Program.cs` registers
  `AddLocalization` (for the embedded fallback),
  `CultureSwitcher` (singleton),
  `HttpBackedStringLocalizer` (singleton, with the
  embedded `IStringLocalizer<SharedResource>` injected
  for the fallback path), and a named `HttpClient`
  (`Cardscape.Resources`) for the same-origin `.resx`
  fetches.

The decision record is
[ADR 0010](../adr/0010-client-side-culture-switcher.md).
The runtime invariant the ADR guards is **the picker
never touches `Thread.CurrentCulture`**. The runtime
culture stays at `CultureInfo.InvariantCulture`; the
Blazor culture-change detection overlay never fires.

### 13.1 Adding a new language

1. Drop a new `SharedResource.<lang>.resx` next to the
   existing `SharedResource.resx` /
   `SharedResource.es.resx`.
2. Add the language code to
   `CultureSwitcher.AvailableCultures` in
   `src/Cardscape.Web/Services/CultureSwitcher.cs`.
3. Add a `CommonLanguage<lang>` key in all the
   existing `.resx` files (English: "Language",
   Spanish: "Idioma", …) and a new
   `CommonLanguage<lang>Display` key per existing
   language that points at the new language's
   display name.
4. Add the corresponding label rendering in
   `Shared/LanguageSwitcher.razor`'s `GetLabel`
   switch.
5. No `Program.cs` change is needed — the
   `<None Pack="true">` directive in
   `Cardscape.Web.csproj:67` ships any new
   `SharedResource.<lang>.resx` as a static web asset
   automatically.

### 13.2 Verification

- Switch the language in the UI from English to
  Spanish. The page does **not** show the Blazor
  culture-change detection overlay.
- Refresh the page. The Spanish strings render on
  `/login` and `/register` (the two pages that are
  the most visible to new users). The choice
  persists in `localStorage` (the dropdown shows
  "Español" after a hard refresh).
- Switch back to English. The English strings render
  on every page.
- Inspect `localStorage` in the browser dev tools.
  The key `Cardscape.Culture` has the value `"es"` or
  `"en"`.
- Inspect the network tab. The first time the user
  switches to Spanish, the browser fetches
  `/Resources/SharedResource.es.resx` over HTTP.
  Subsequent switches to the same culture are served
  from the picker's in-memory dictionary.

---

## 14. When to revisit

This document is revisited when:

1. A per-phrase i18n framework is added.
2. A drift detector is added.
3. A new language is added.
4. A new artifact type is translated (e.g. the MCP
   server's prompts).
5. The `.resx` placeholder convention (§11.4) needs to
   change (e.g. if a non-todo workflow is introduced).
6. The picker grows a per-page language override (a
   page that wants to render English strings even when
   the user prefers Spanish — the docs site, for
   example).

Until then, this document is the source of truth for the
translation workflow in Cardscape.
