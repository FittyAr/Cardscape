# 0010 — Client-side culture switcher (Blazor WebAssembly)

> **Status**: Accepted
> **Date**: 2026-08-04
> **Supersedes**: (none)
> **Related**: [ADR 0009 — Radzen-only UI](0009-radzen-only-ui.md), [docs/i18n/02-translation-workflow.md §12](../i18n/02-translation-workflow.md#12-blazor-webassembly-culture-resolution-caveat), [docs/roadmap/05-plan-v1.2.0.md §3 (D7)](../roadmap/05-plan-v1.2.0.md#3-priority-2--i18n-follow-up-d7)

## 1. Context

Cardscape.Web is a Blazor WebAssembly client. The project
ships with English (default) + Spanish translations in
`src/Cardscape.Web/Resources/SharedResource.{resx,es.resx}`.
The v1.1.0 audit (G12) tried to wire
`SetDefaultCulture` / `AddSupportedCultures` +
`CultureInfo.DefaultThreadCurrentCulture` so the runtime
culture could be switched by an end user from the UI.
That push was reverted because the .NET 10 SDK triggers
the *"Blazor detected a change in the application's
culture that is not supported with the current project
configuration"* overlay on every F5 refresh, and the
overlay does not go away. The push also surfaced two
second-order issues:

1. The `Microsoft.NET.Sdk.BlazorWebAssembly` SDK does
   not reference the full `Microsoft.AspNetCore.App`
   shared framework, so `RequestLocalizationOptions`
   (which hosts `SetDefaultCulture` /
   `AddSupportedCultures`) is not available.
2. Adding `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
   fails with `NETSDK1082` (no `browser-wasm`
   runtime pack).
3. The standalone `Microsoft.AspNetCore.Localization`
   NuGet package tops out at 2.3.11 (ASP.NET Core 2.x
   era) and is not compatible with the .NET 10 SDK.

The v1.1.0 G12 push left the Spanish `.resx` as a
static web asset under `wwwroot/Resources/` so a future
client-side culture picker could load it over HTTP. The
picker is the subject of this ADR.

## 2. Decision

**Cardscape.Web uses a client-side `CultureSwitcher`
service that loads translations from static web
assets and feeds them to a custom
`IStringLocalizer` over an in-memory dictionary.
The runtime culture stays at
`CultureInfo.InvariantCulture`; the Blazor
culture-change detection overlay never fires.**

The picker is a singleton service. It owns:

- The current culture (a string, default `"en"`).
- A per-culture in-memory dictionary of `name → value`
  pairs.
- The persistence to `localStorage` under the key
  `Cardscape.Culture`.

The `HttpBackedStringLocalizer` (the custom
`IStringLocalizer` registered with DI as the
implementation for `IStringLocalizer<SharedResource>`)
reads from the dictionary. If the dictionary does not
have the key, it falls back to the standard
`StringLocalizer<SharedResource>` that reads the
embedded English `SharedResource.resx`. The fallback
ensures the first render is never empty — the picker
has not yet loaded the static `.resx` from the
server, but the English strings are right there in
the assembly.

The language switcher in the layout is a small
`RadzenDropDown` (`Shared/LanguageSwitcher.razor`) that
calls `CultureSwitcher.SetCultureAsync(culture)` on
change. The picker:

1. Persists the new culture to `localStorage`.
2. Fetches `Resources/SharedResource.{culture}.resx`
   over `HttpClient` and parses the `<data name=…>`
   elements into the dictionary.
3. Raises a `Changed` event that the layout listens to
   and converts into a `StateHasChanged()`.

The `IStringLocalizer` is registered as the singleton
implementation for `IStringLocalizer<SharedResource>`
in `Program.cs`. Every `@L["…"]` expression in the
`.razor` files re-evaluates on the next render and
picks up the new translation.

## 3. Rationale

### Why not the standard `AddLocalization` + `SetDefaultCulture` path?

The .NET 10 SDK does not support
`SetDefaultCulture` / `AddSupportedCultures` on the
Blazor WebAssembly SDK. The community workarounds
(e.g. `Microsoft.AspNetCore.Localization` standalone
NuGet) are not compatible with the .NET 10 BCL, and
adding the full `Microsoft.AspNetCore.App` framework
reference to the WASM project fails with `NETSDK1082`.
The v1.1.0 G12 push verified all three workarounds and
reverted the change with a "WASM caveat" doc note in
`docs/i18n/02-translation-workflow.md §12`.

### Why not `CultureInfo.DefaultThreadCurrentCulture`?

The Blazor runtime detects the culture change and
shows the *"Blazor detected a change in the
application's culture that is not supported with the
current project configuration"* overlay on every F5
refresh. The .NET 10 SDK does not expose a way to
suppress the detection. The only way to avoid the
overlay is to not change the runtime culture at all.

### Why the dictionary-backed `IStringLocalizer`?

The standard `StringLocalizer<T>` reads from the
embedded `.resources` manifest. Replacing the entire
`StringLocalizer<T>` pipeline would break the
fallback (the first render before the picker has
loaded the static `.resx`). The dictionary-backed
localizer reads from the picker when the key is
present, and falls back to the embedded manifest
otherwise. The result is a transparent switch from
"embedded English" to "loaded Spanish" (or back) on
the next render.

### Why the `HttpClient` named client?

The default `HttpClient` is fine for the
client-side request, but the project's `Cardscape.Api`
named client carries the API base URL and the bearer
token. The picker fetches a same-origin relative URL
(`/Resources/SharedResource.es.resx`), so using the
default would 401 against the API base URL. The
`Cardscape.Resources` named client uses
`builder.HostEnvironment.BaseAddress` and is the
correct carrier for static web asset fetches.

## 4. Consequences

### Positive

- The Spanish translations ship as a static web asset
  (already wired by the v1.1.0 G12 push). The picker
  is the consumer.
- Switching the language in the UI does not trigger
  the Blazor culture-change detection overlay.
- The choice persists in `localStorage` and survives
  page refreshes.
- The first render is never empty (the embedded
  English strings are the fallback).
- No `IJSRuntime.InvokeAsync` for the language
  switcher (the picker uses `InvokeAsync` only for
  `localStorage` reads/writes).
- No new external dependency. The picker is built
  on `HttpClient` (already in the project) and
  `IJSRuntime` (built into Blazor).
- The picker is testable: the
  `HttpBackedStringLocalizer` is a pure DI service
  with no static state, so future maintainers can
  swap in a fake `IStringLocalizer<SharedResource>`
  and a fake `CultureSwitcher` for unit tests.

### Negative / trade-offs

- The dictionary loads asynchronously. The first
  render after a language change shows the previous
  language's strings for one render cycle. This is
  the same behaviour as every other i18n
  implementation (the standard
  `RequestLocalization` middleware also has a
  first-render window).
- `localStorage` is per-browser. A user who clears
  the cache, switches browsers, or uses incognito
  mode falls back to the default culture ("en"). This
  is acceptable for a self-hosted kanban.
- The picker is a `Microsoft.AspNetCore.Components`
  concept. The picker class lives in
  `Cardscape.Web.Services` and is not reusable from
  the API. That is fine; the API is server-side and
  uses the API's own culture resolution (which is
  the user's `Accept-Language` header).
- The picker is not yet tested with bUnit. The v1.2.0
  plan §8 leaves a bUnit setup as v1.3.0 work. For
  now the verification is the manual smoke test (set
  the dropdown to Spanish, refresh, confirm the
  Spanish strings render on `/login` and
  `/register`).

## 5. Compliance

This decision is enforced by:

1. The `BlazorWebAssemblyLoadAllGlobalizationData`
   property in `Cardscape.Web.csproj:15` — required
   for any non-default culture data to boot without
   crashing. The picker does not change the runtime
   culture, but the data is still loaded so the
   `DateTime` / `Number` formatters that come up in
   the UI (e.g. the `ToString("d")` calls in the
   card detail page) work for any culture the
   browser sends in `Accept-Language`.
2. The `<None Pack="true">` directive in
   `Cardscape.Web.csproj:67` — ships
   `SharedResource.es.resx` as a static web asset
   under `wwwroot/Resources/`. The picker fetches
   this exact path.
3. The shared component
   `Shared/LanguageSwitcher.razor` is the only UI
   surface that calls `CultureSwitcher.SetCultureAsync`.
   The switcher is rendered in both the `<Authorized>`
   and `<NotAuthorized>` branches of the layout, so
   the user can change the language before and after
   sign-in.

The runtime invariant — the picker never touches
`Thread.CurrentCulture` — is the design centre. A
future maintainer who adds a code path that mutates
the runtime culture to "support the picker" is
re-introducing the Blazor culture-change detection
overlay. The ADR is the guard rail.
