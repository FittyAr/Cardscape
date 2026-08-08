# 02 — Plan de ejecución: refactor UI → Radzen puro

> **Status**: ✅ **Completed** (2026-08-04). Este plan describe
> el trabajo **tal como se planeó**. Lo que realmente se
> construyó está en [`README.md`](README.md) y
> [ADR 0009](../adr/0009-radzen-only-ui.md). El siguiente chunk
> de trabajo es [`docs/roadmap/05-plan-v1.2.0.md`](../roadmap/05-plan-v1.2.0.md).

> **Lee primero** [`01-audit.md`](01-audit.md) — ahí está el
> inventario completo del estado actual. Este documento es la
> **estrategia de ejecución**.
>
> **TL;DR**:
> - 3 oleadas (P0 → P1 → P2), **3-4 sesiones** de trabajo.
> - **6 componentes shared nuevos** a crear (5 si se decide
>   auto-hostear fuentes).
> - **~1100 líneas de CSS** eliminables, **~250** movibles a
>   CSS isolation, **~3 MB** de assets a borrar.
> - Cada PR tiene un **checklist de aceptación** y un
>   **commit message template** sugerido.
>
> **Convención de branches**: `refactor/radzen-<oleada>-<slug>`
> (ej. `refactor/p0-seguridad-js`, `refactor/p1-shared-components`).
>
> **Convención de commits**: Conventional Commits
> (`refactor(web): ...`).

---

## 0. Reglas globales (válidas para todos los PRs)

Estas reglas vienen de [`01-audit.md` §README](README.md) y se
repite acá para que cada PR se auto-verifique:

1. **Cero clases custom en `app.css`** que no estén dentro
   de `:root`, `#blazor-error-ui`, `.blazor-error-boundary` o
   `.loading-progress*` (los 4 elementos del template WASM).
2. **Cero `<button>`, `<input>`, `<form>`, `<a href="...">`
   (navegación interna) en `.razor`** — siempre
   `RadzenButton`/`RadzenTextBox`/`RadzenTemplateForm`/
   `RadzenLink`/`NavLink`.
3. **Cero `IJSRuntime.InvokeAsync`** sin ADR justificando.
4. **CSS isolation** (`.razor.css`) para los 3 componentes
   shared que Radzen no cubre. Documentar el motivo.
5. **Build verde, tests verdes, WCAG AA preservado**
   (`prefers-reduced-motion` extendido a las animaciones Radzen).
6. **No tocar archivos fuera del scope del PR**. Si encontrás
   custom UI en una página que no es la del PR, abrir issue y
   dejar para P1/P2 (no scope creep).
7. **Respetar convenciones** de
   [`docs/development/01-conventions.md`](../development/01-conventions.md)
   (file-scoped namespaces, `var` para built-ins, async all the
   way, no `void` async, etc.).
8. **Cargar `.agents/skills/radzen-blazor/SKILL.md`** antes de
   cualquier PR que toque UI. Esa skill es la fuente de verdad
   de los componentes Radzen (per
   [`docs/AGENTS.md` §8](../AGENTS.md#8-available-agent-skills-project-local)).

---

## 1. Pre-requisitos (antes de la Oleada 0)

### PR-0.1 — Setup de métricas base

- **Objetivo**: tener una línea base medible del estado actual
  para verificar progreso.
- **Acciones**:
  1. Crear un test `xunit` (o un script PowerShell) que cuente:
     - `<button` / `<input` / `<form` / `<a href` en `Pages/*.razor`
     - `IJSRuntime.InvokeAsync` en `Pages/*.razor`
     - Líneas de `app.css` (total y por bloque)
     - MB de `wwwroot/lib/bootstrap/`
  2. Imprimir la tabla resumen en consola al ejecutar
     `dotnet test --filter "Category=UiMetrics"` o el script.
  3. Documentar los números iniciales en este `02-plan.md` (al
     final, sección "Métricas").
- **Por qué**: el refactor grande necesita poder afirmar
  "el `<button>` count bajó de 2 a 0" o "el CSS bajó de 1517
  a 200 líneas" sin contar a mano.
- **Esfuerzo**: **S** (1-2h).
- **Riesgo**: ninguno.

---

## 2. Oleada 0 (P0) — Seguridad y blockers visuales

**Duración estimada**: 1 sesión (4-6h).

**Criterio de salida de la oleada**: cero `IJSRuntime` en
`Pages/`, cero `<button>`/`<form>`/`<input>` con clases
custom, `manifest.webmanifest` alineado, `<script type="importmap">`
eliminado, Bootstrap fuera de `wwwroot/`.

### PR-0.2 — Eliminar `IJSRuntime` con `eval` (XSS vector)

- **Archivo**: `Pages/OAuthCallback.razor` (L39, L48)
- **Cambio**:
  - Reemplazar el bloque `OnAfterRenderAsync` + `JS.InvokeAsync<string>("eval", "window.location.href")` por lectura directa de `Nav.Uri`:
    ```csharp
    protected override void OnInitialized()
    {
        var uri = new Uri(Nav.Uri);
        var fragment = uri.Fragment.TrimStart('#');
        // ... lógica existente con `fragment` en lugar del eval
    }
    ```
  - Confirmar que `Nav` (NavigationManager) ya está inyectado.
  - Si no, agregarlo a la lista de `@inject`.
- **Verificación**:
  - `grep -r "IJSRuntime" src/Cardscape.Web/` debe dar 0
    ocurrencias en `Pages/`.
  - `grep -r "\beval\b" src/Cardscape.Web/` debe dar 0.
  - Probar el flujo OAuth manualmente (Google + Microsoft +
    Apple) en dev.
- **Esfuerzo**: **S** (30-60 min).
- **Commit**: `fix(web)!: replace JS eval with NavigationManager in OAuthCallback (XSS surface)`.

### PR-0.3 — Reemplazar `prompt()` nativo por `RadzenDialog`

- **Archivo**: `Pages/SettingsTwoFactor.razor` (L80, L131)
- **Cambio**:
  - Crear un componente inline `ConfirmCodeDialog.razor` en
    `Shared/` (o usar `DialogService.OpenAsync` con
    parámetros) que muestre un `RadzenTemplateForm` con un
    `RadzenTextBox` para el código.
  - Reemplazar el `JS.InvokeAsync<string>("prompt", ...)` por
    `await DialogService.OpenAsync<ConfirmCodeDialog>(...)` con
    `CloseDialogMode = CloseDialogMode.ClickOutside` deshabilitado.
- **Verificación**:
  - Test manual del flujo de regenerar códigos 2FA.
  - `grep -r "IJSRuntime" src/Cardscape.Web/Pages/` debe dar 0.
- **Esfuerzo**: **M** (1-2h).
- **Commit**: `refactor(web): replace window.prompt with RadzenDialog in 2FA settings`.

### PR-0.4 — Limpiar `OAuthCallback.razor` (HTML + CSS)

- **Archivo**: `Pages/OAuthCallback.razor` (L17-34)
- **Cambio**:
  - Reemplazar `<div class="auth-shell"><div class="auth-card--status">` por `<RadzenCard class="rz-p-6">`.
  - Reemplazar `<a class="provider-button" href="login">` por `<RadzenButton Text="@L["AuthBackToSignIn"]" ButtonStyle="ButtonStyle.Primary" Click="@(() => Nav.NavigateTo("login"))" />`.
  - Reemplazar `<div class="auth-spinner">` por `<RadzenProgressBarCircular Mode="ProgressBarMode.Indeterminate" Size="ProgressBarCircularSize.Medium" />`.
- **Verificación**: visual en /oauth/callback.
- **Esfuerzo**: **S** (30-60 min).
- **Commit**: `refactor(web): use Radzen primitives in OAuthCallback`.

### PR-0.5 — Migrar `Register.razor` a Radzen puro

- **Archivo**: `Pages/Register.razor` (L14-58)
- **Cambio**:
  - L14-19: `<div class="auth-shell"><div class="auth-card"><header class="auth-header"><h1 class="auth-title"><p class="auth-subtitle">` → `<RadzenCard class="rz-p-6">` con `RadzenText TextStyle="TextStyle.H4" TagName="TagName.H1"` + `RadzenText TextStyle="TextStyle.Body2" class="rz-color-text-secondary"`.
  - L22, 28, 34: `<div class="auth-field">` + `RadzenLabel` + `RadzenTextBox` → `RadzenFormField` (mismo patrón que `Login.razor:48-55`).
  - L38: `<small class="auth-hint">` → `RadzenText TextStyle="TextStyle.Caption" class="rz-color-text-secondary"`.
  - L43: `<div class="auth-error" role="alert">` → `RadzenAlert AlertStyle="AlertStyle.Danger" Variant="Variant.Flat" AllowClose="false"`.
  - L46-51: `class="auth-submit"` → `Style="width:100%"` (o `class="rz-w-100"`).
  - L54-56: `<p class="auth-footer">...<a href="login">` → `RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Center" Gap="0.25rem"` con `RadzenText` + `RadzenLink Path="login"`.
- **Verificación**:
  - Crear cuenta nueva manualmente.
  - Comparar visualmente con `Login.razor` (deben verse consistentes).
- **Esfuerzo**: **M** (1-2h).
- **Commit**: `refactor(web): use Radzen primitives in Register`.

### PR-0.6 — Limpiar `index.html` y `manifest.webmanifest`

- **Archivos**: `wwwroot/index.html` (L17), `wwwroot/manifest.webmanifest` (L9)
- **Cambio**:
  - `index.html:17`: eliminar `<script type="importmap"></script>` (vacío).
  - `manifest.webmanifest:9`: alinear `theme_color` con `index.html:16` (`#0f3d3e`). Opcional: alinear `background_color` también.
- **Verificación**: el sitio se ve igual en el browser, el manifest pasa el validator de PWA.
- **Esfuerzo**: **S** (15 min).
- **Commit**: `chore(web): remove empty importmap script and sync manifest theme color`.

### PR-0.7 — Eliminar Bootstrap de `wwwroot/lib/`

- **Acción**: borrar el directorio completo
  `wwwroot/lib/bootstrap/` (3 MB).
- **Verificación**:
  - `grep -r "bootstrap" src/Cardscape.Web/` debe dar 0 (excepto
    posiblemente en `obj/` que se regenera).
  - `dotnet build` y `dotnet test` siguen verdes.
  - El sitio funciona visualmente (ninguna clase `btn-*` /
    `form-*` / `container` / `row` debe estar usándose en
    runtime, ya auditamos que no).
- **Esfuerzo**: **S** (15 min).
- **Commit**: `chore(web): remove unused Bootstrap 5 assets (-3 MB)`.

### ✅ Checkpoint Oleada 0

```bash
# Verificación automatizable (ejecutar y pegar output en el PR)
grep -r "IJSRuntime" src/Cardscape.Web/Pages/                # → 0
grep -rE '<button|<input|<form' src/Cardscape.Web/Pages/    # → 0
ls -la src/Cardscape.Web/wwwroot/lib/bootstrap               # → No such file or directory
wc -l src/Cardscape.Web/wwwroot/css/app.css                 # → < 1450 (mata auth-sh* y provider-*)
dotnet test --filter "Category=UiMetrics"                   # → ver métricas base
```

---

## 3. Oleada 1 (P1) — Componentes shared + refactor por bloque

**Duración estimada**: 2-3 sesiones (8-12h).

**Estrategia**: primero se crean los **componentes shared**
nuevos (PageHeader, LabeledField, MetadataList) en un PR
aislado. Después se hace el refactor masivo de páginas en PRs
temáticos. Los **componentes de vista custom** (KanbanBoard,
MonthCalendar, MonthPlanner) se hacen al final porque son los
más complejos.

**Criterio de salida de la oleada**:
- 6 componentes shared nuevos en `Shared/`.
- 26 páginas refactorizadas (de las 33 totales).
- 800+ líneas eliminadas de `app.css`.
- 0 referencias a `auth-error`, `auth-shell`, `auth-card`,
  `auth-field`, `auth-hint`, `auth-footer`, `provider-button`,
  `inbox-item`, `rule-card`, `token-card`, `invitation-card`,
  `member-card` en `.razor`.

### PR-1.1 — Crear `Shared/PageHeader.razor`

- **Componente nuevo**:
  ```razor
  @* Shared/PageHeader.razor *@
  @* Header consistente para todas las páginas (no-auth).
       Reemplaza el patrón repetido:
         <div class="page-shell">
           <div class="page-header">
             <h1>...</h1>
             <PageHeaderActions>...</PageHeaderActions>
           </div>
         </div>
  *@
  @code {
      [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
      [Parameter] public string? Subtitle { get; set; }
      [Parameter] public RenderFragment? Actions { get; set; }
  }
  ```
- **Markup sugerido**:
  ```razor
  <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" JustifyContent="JustifyContent.SpaceBetween" Gap="1rem" class="rz-mb-4">
      <RadzenStack Gap="0.25rem">
          <RadzenText Text="@Title" TextStyle="TextStyle.H4" TagName="TagName.H1" />
          @if (!string.IsNullOrEmpty(Subtitle))
          {
              <RadzenText Text="@Subtitle" TextStyle="TextStyle.Body2" class="rz-color-text-secondary" />
          }
      </RadzenStack>
      @if (Actions is not null)
      {
          <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
              @Actions
          </RadzenStack>
      }
  </RadzenStack>
  ```
- **CSS isolation opcional** (`Shared/PageHeader.razor.css`) si se
  quiere agregar un border-bottom o spacing custom. Mantenerlo
  mínimo.
- **Verificación**: compila, renderiza en un test manual. No se
  usa todavía en este PR (eso es PR-1.4+).
- **Esfuerzo**: **S** (30 min).
- **Commit**: `refactor(web): add shared PageHeader component`.

### PR-1.2 — Crear `Shared/LabeledField.razor`

- **Componente nuevo**:
  ```razor
  @* Shared/LabeledField.razor *@
  @* Encapsula el patrón:
       <div class="auth-field">
         <RadzenLabel Text="..." Component="..." />
         <RadzenTextBox Name="..." ... />
       </div>
     que aparece ~30 veces en 12 páginas.
  *@
  @code {
      [Parameter, EditorRequired] public string Text { get; set; } = string.Empty;
      [Parameter, EditorRequired] public string Component { get; set; } = string.Empty;
      [Parameter] public RenderFragment ChildContent { get; set; } = null!;
      [Parameter] public string? Help { get; set; }
  }
  ```
- **Markup sugerido**:
  ```razor
  <RadzenStack Gap="0.35rem" class="rz-mt-2">
      <RadzenLabel Text="@Text" Component="@Component" />
      @ChildContent
      @if (!string.IsNullOrEmpty(Help))
      {
          <RadzenText Text="@Help" TextStyle="TextStyle.Caption" class="rz-color-text-secondary" />
      }
  </RadzenStack>
  ```
- **Decisión**: este componente es **wrapper de `RadzenFormField`**
  con spacing consistente. Si se prefiere, se puede obviar y usar
  `RadzenFormField` directamente — pero ese requiere que el
  control sea su `ChildContent` (que ya soporta), y la principal
  ventaja de `LabeledField` es que ya tiene el `Gap="0.35rem"` +
  opcional `Help` caption que se repite en muchas páginas.
- **Verificación**: compila.
- **Esfuerzo**: **S** (30 min).
- **Commit**: `refactor(web): add shared LabeledField component`.

### PR-1.3 — Crear `Shared/MetadataList.razor`

- **Componente nuevo**:
  ```razor
  @* Shared/MetadataList.razor *@
  @* Reemplaza el patrón <dl class="card-meta"> con dt/dd
       que aparece en CardDetail.razor L79-101 y L163-169.
  *@
  @code {
      [Parameter, EditorRequired] public IReadOnlyList<KeyValuePair<string, RenderFragment>> Items { get; set; } = Array.Empty<KeyValuePair<string, RenderFragment>>();
  }
  ```
- **Markup sugerido**:
  ```razor
  <RadzenStack Gap="0.5rem" class="rz-mt-2">
      @foreach (var item in Items)
      {
          <RadzenRow AlignItems="AlignItems.Start" Gap="1rem">
              <RadzenColumn Size="3">
                  <RadzenText Text="@item.Key" TextStyle="TextStyle.Body2" class="rz-color-text-secondary" />
              </RadzenColumn>
              <RadzenColumn Size="9">
                  @item.Value
              </RadzenColumn>
          </RadzenRow>
      }
  </RadzenStack>
  ```
- **CSS isolation opcional** (`Shared/MetadataList.razor.css`) para
  un look más "dl/dt" si hace falta.
- **Verificación**: compila.
- **Esfuerzo**: **S** (30 min).
- **Commit**: `refactor(web): add shared MetadataList component`.

### PR-1.4 — Refactor Workspaces (Pages/Workspaces.razor)

- **Cambios**:
  - Header `page-shell` + `page-header` → `<PageHeader Title="@L["WorkspacesTitle"]" />` con `Actions` para el botón de crear.
  - Grid de cards → `RadzenRow Gap="1rem"` + `RadzenColumn Size="12" SizeMD="6" SizeLG="4"`.
  - `<div class="auth-error">` → `RadzenAlert`.
- **Verificación**: visual, crear/editar/borrar workspace.
- **Esfuerzo**: **M** (1-2h).
- **Commit**: `refactor(web): use Radzen layout primitives in Workspaces page`.

### PR-1.5 — Refactor Boards (Pages/Boards.razor)

- Idem PR-1.4 con grid de boards.
- **Esfuerzo**: **M**.
- **Commit**: `refactor(web): use Radzen layout primitives in Boards page`.

### PR-1.6 — Refactor Inbox + Invitations + Activity (productivity)

- **Archivos**: `Pages/Inbox.razor`, `Pages/Invitations.razor`, `Pages/Activity.razor`
- **Patrón**:
  - Header → `PageHeader`.
  - Listas de cards → `RadzenDataGrid` o `RadzenCard Variant="Variant.Flat"` con `Style="border-left: 3px solid ..."`.
  - `<div class="auth-error">` → `RadzenAlert`.
  - `<a href="...">` interno → `RadzenLink`.
  - `<small>` → `RadzenText`.
- **Esfuerzo**: **M** cada uno.
- **Commit**: 3 commits separados (uno por página) o un commit
  batch con el sufijo `(productivity pages)`.

### PR-1.7 — Refactor Auth legacy cleanup (CSS)

- **Archivos**: `wwwroot/css/app.css`
- **Cambio**: eliminar líneas **185-522** (todo el bloque
  `.auth-page`, `.auth-brand*`, `.auth-shell`, `.auth-card*`,
  `.auth-header`, `.auth-title`, `.auth-subtitle`, `.auth-form`,
  `.auth-field`, `.auth-submit`, `.auth-back-link`,
  `.auth-error`, `.auth-hint`, `.auth-footer`,
  `.auth-providers`, `.provider-button`, `.provider-glyph`,
  `.provider-google/microsoft/apple`, `.auth-divider`,
  `.auth-spinner`, `@keyframes cs-rise`, `@keyframes cs-spin`,
  `.auth-*` responsive) **solo si** los PRs PR-0.4, PR-0.5 y
  PR-2.4 (SettingsExternalLogins) ya están mergeados.
- **Verificación**:
  - `grep -r "class=\"auth-" src/Cardscape.Web/` debe dar 0.
  - `grep -r "class=\"provider-" src/Cardscape.Web/` debe dar 0.
- **Esfuerzo**: **S** (15 min, son puras eliminaciones).
- **Commit**: `chore(web): remove dead auth/landing CSS block (-300 lines)`.

### PR-1.8 — Refactor Home + landing legacy cleanup (CSS)

- **Archivos**: `Pages/Home.razor` (ya es Radzen, solo limpieza
  cosmética), `wwwroot/css/app.css` (líneas 524-665 y 764-896).
- **Cambio**:
  - `Home.razor`: normalizar `Style="..."` inline (minúsculas,
    claves de spacing Radzen).
  - `app.css`: eliminar `.user-info`, `.user-name`, `.user-email`,
    `.top-link`, `.page-shell`, `.page-header`, `.page-header h1`,
    `.home-*`, `.landing-*`.
- **Verificación**: visual en home autenticado y no-autenticado.
- **Esfuerzo**: **S** (30 min).
- **Commit**: `chore(web): remove dead home/landing CSS blocks (-500 lines)`.

### PR-1.9 — Refactor WorkspaceMembers (DataGrid doble)

- **Archivo**: `Pages/WorkspaceMembers.razor`
- **Cambio**:
  - L74-86: `<div class="member-list">` con RadzenCards →
    `RadzenDataGrid TItem="WorkspaceMemberDto"` con
    `Data=@members`, `AllowSorting="true"`, `AllowFiltering="true"`,
    `AllowPaging="true"`, `PageSize="20"`. Columnas:
    `Avatar` (RadzenImage o iniciales), `Name`, `Email`, `Role`
    (RadzenBadge), `Actions` (RadzenButton con menú).
  - L100-115: idem para `<div class="invitation-list">` con
    `TItem="WorkspaceInvitationDto"`.
  - L39-48: `<div class="issued-secret">` con `<pre class="secret-box">` →
    `RadzenAlert AlertStyle="AlertStyle.Warning"` con `<code>` adentro.
  - L52, 67: `<div class="auth-error">` → `RadzenAlert`.
- **Verificación**: invitar, revocar, ver lista.
- **Esfuerzo**: **L** (3-4h, primera vez configurando DataGrid en el proyecto).
- **Commit**: `refactor(web): use RadzenDataGrid for workspace members and invitations`.

### PR-1.10 — Refactor WorkspaceImport + WorkspaceSaml + WorkspaceScim + WorkspaceSlack + WorkspaceIntegrations + WorkspaceEmail (sin tablas)

- **Archivos**: 6 páginas
- **Patrón**:
  - Header → `PageHeader`.
  - `RadzenLabel` + `RadzenTextBox` separados → `LabeledField` o `RadzenFormField`.
  - `<div class="create-actions">` → `RadzenStack Orientation="Horizontal" JustifyContent="End" Gap="0.5rem"`.
  - `<div class="auth-error">` → `RadzenAlert`.
  - Clases huérfanas (`integration-grid`, `scim-token-card`,
    `form-row`, `form-actions`, `import-status`,
    `success-banner`) → reemplazo Radzen directo.
- **Verificación**: cada página, cada acción (import, saml setup,
  scim token, slack connect, integrations list, email setup).
- **Esfuerzo**: **M** total (30-60 min cada una).
- **Commit**: un commit por página, ej.
  `refactor(web): use Radzen primitives in WorkspaceSaml page`.

### PR-1.11 — Refactor WorkspaceGitHub + WorkspaceEmail + SettingsOAuthApps (tablas → DataGrid)

- **Archivos**: 3 páginas
- **Cambio**:
  - `<table class="cards-table">` y `<table class="oauth-apps-table">` →
    `RadzenDataGrid` con `TItem=` correspondiente.
  - Mantener las columnas que ya existen (Number/Title/State para
    PRs, Email/Status para emails, Name/Scopes/Status/Actions para
    OAuth apps).
  - Status pill → `RadzenBadge` condicional.
- **Verificación**: cada tabla, scroll, sort, paginate, filter.
- **Esfuerzo**: **L** total (2-3h cada una).
- **Commit**: un commit por página, ej.
  `refactor(web): use RadzenDataGrid in SettingsOAuthApps page`.

### PR-1.12 — Refactor ApiTokens (DataGrid + form)

- **Archivo**: `Pages/ApiTokens.razor`
- **Cambio**:
  - Header → `PageHeader`.
  - Form con `RadzenCheckBox` + `RadzenLabel` separados →
    `RadzenFormField` con `RadzenCheckBox` adentro.
  - `<div class="issued-secret">` con `<pre class="secret-box">` →
    `RadzenAlert AlertStyle="AlertStyle.Warning"`.
  - `<div class="token-list">` con cards → `RadzenDataGrid TItem="ApiTokenSummaryDto"`.
- **Esfuerzo**: **L** (2-3h).
- **Commit**: `refactor(web): use RadzenDataGrid and RadzenFormField in ApiTokens page`.

### PR-1.13 — Refactor CustomFields + Automation (DataGrid + form)

- **Archivos**: `Pages/CustomFields.razor`, `Pages/Automation.razor`
- **Patrón**: mismo que ApiTokens — DataGrid + FormField +
  Alerts + Stack primitives.
- **Esfuerzo**: **L** cada una.
- **Commit**: un commit por página.

### PR-1.14 — Refactor SettingsGoogleDrive + SettingsTwoFactor (form, no DataGrid)

- **Archivos**: 2 páginas
- **Patrón**: `PageHeader` + `RadzenFormField` + `RadzenAlert` +
  `RadzenText` (en lugar de `<h2>`/`<h3>` crudos).
- **Esfuerzo**: **S-M** cada una.
- **Commit**: un commit por página.

### PR-1.15 — Refactor SettingsExternalLogins (provider buttons)

- **Archivo**: `Pages/SettingsExternalLogins.razor`
- **Cambio**:
  - L19-32: `<div class="provider-buttons">` con 3 `<a class="provider-button">` →
    3 `RadzenButton ButtonStyle="ButtonStyle.Light"` con
    `Icon="cloud_circle"` (Google), `Icon="phonelink"`
    (Microsoft), `Icon="phone_iphone"` (Apple). Mantener el
    texto de cada provider.
  - Si se quieren los "color boxes" de cada provider, crear
    un `Shared/ProviderButton.razor` con CSS isolation que
    muestre un cuadrado de color al lado del icono.
- **Verificación**: conectar/desconectar cada provider.
- **Esfuerzo**: **M** (1-2h).
- **Commit**: `refactor(web): use RadzenButton for external login providers`.

### PR-1.16 — Refactor BoardDashboard + BoardExtensions + MirrorCardDialog

- **Archivos**: 3 páginas
- **Patrón**: `PageHeader` + `RadzenCard Variant="Variant.Flat"` +
  `LabeledField` + `RadzenAlert` + `RadzenStack` (en lugar de
  `extension-row`, `extension-meta`, `mirror-card-dialog`,
  `mirror-card-actions`, `dashboard-grid`).
- **Esfuerzo**: **S-M** cada una.
- **Commit**: un commit por página.

### PR-1.17 — Refactor CardDetail (la página más larga)

- **Archivo**: `Pages/CardDetail.razor`
- **Cambio** (siguiendo [`01-audit.md` §2.5](01-audit.md#25-boards--cards)):
  - L24-32: shell → `class="rz-mx-auto"` + `Style="max-width:48rem"`.
  - L32-34: header → `RadzenStack`.
  - L33: `<h1>` → `RadzenText TextStyle="TextStyle.H5"`.
  - L47-76: actions + vote-count → `RadzenBadge` / `RadzenText`.
  - L79-101 + L163-169: `<dl class="card-meta">` → `<MetadataList>`.
  - L130, 133, 179: `style="..."` inline → clases Radzen.
  - L107-123: snooze section → `RadzenStack`.
  - L235-271: checklist (clases huérfanas) → `RadzenCard` + `RadzenStack`.
  - L274-294: activity list → `RadzenTimeline` o `RadzenDataGrid`.
- **Verificación** exhaustiva: editar card, agregar comentario,
  votar, checklist, actividad, snooze, mirror.
- **Esfuerzo**: **L** (4-6h, página más larga y crítica).
- **Commit**: `refactor(web): use Radzen primitives in CardDetail page`.

### PR-1.18 — Refactor BoardDetail (header + live indicator)

- **Archivo**: `Pages/BoardDetail.razor`
- **Cambio** (sin tocar el kanban todavía, eso es PR-1.20):
  - L16-50: header + live indicator + description + actions.
  - L26-29: `<span class="live-indicator">` con `<span class="dot">` →
    `RadzenBadge BadgeStyle="BadgeStyle.Success" Text="● Live"`.
  - L36-39: `<p class="board-description">` → `RadzenText`.
  - L54-62: `<RadzenCard class="add-list-form">` →
    `RadzenCard Variant="Variant.Flat"`.
- **Verificación**: visual en /boards/{id}, indicador live
  funciona (SignalR activo).
- **Esfuerzo**: **M** (1-2h).
- **Commit**: `refactor(web): use Radzen primitives in BoardDetail header`.

### PR-1.19 — Refactor AcceptInvitation + OAuthCallback polish

- **Archivos**: 2 páginas
- **Cambio**:
  - `AcceptInvitation.razor`: header → `PageHeader` + `RadzenAlert` + `RadzenText` secondary.
  - `OAuthCallback.razor`: aplicar `PageHeader` también (consistencia).
- **Esfuerzo**: **S** cada una.
- **Commit**: un commit por página.

### ✅ Checkpoint Oleada 1

```bash
grep -rE 'class="(auth-|home-|landing-|workspace-grid|board-grid|user-info|page-shell|page-header)"' src/Cardscape.Web/Pages/   # → 0
wc -l src/Cardscape.Web/wwwroot/css/app.css                  # → < 400
ls src/Cardscape.Web/Shared/*.razor                          # → 6+ archivos (PageHeader, LabeledField, MetadataList + existentes)
dotnet test --filter "Category=UiMetrics"                    # → ver métricas
```

---

## 4. Oleada 1.5 (P1) — Componentes shared de vista custom

**Duración estimada**: 1-2 sesiones (4-8h).

**Criterio de salida**: 3 componentes shared nuevos
(`KanbanBoard`, `MonthCalendar`, `MonthPlanner`) con su CSS
isolation, reemplazando los CSS custom en `app.css:1015-1476`.

### PR-1.20 — Crear `Shared/KanbanBoard.razor`

- **Componente nuevo** parametrizable:
  ```razor
  @* Shared/KanbanBoard.razor *@
  @* Vista kanban con scroll horizontal, columnas con header,
       cards mini. Reemplaza el CSS de app.css:1015-1072 (58 líneas)
       y la markup de BoardDetail.razor L65-116.
  *@
  @typeparam TItem
  @code {
      [Parameter, EditorRequired] public IReadOnlyList<KanbanColumn<TItem>> Columns { get; set; } = new List<KanbanColumn<TItem>>();
      [Parameter] public RenderFragment<KanbanColumn<TItem>>? ColumnHeader { get; set; }
      [Parameter] public RenderFragment<TItem>? CardTemplate { get; set; }
      [Parameter] public EventCallback<KanbanCardDropEvent<TItem>> OnCardDrop { get; set; }
  }
  ```
- **CSS isolation** (`Shared/KanbanBoard.razor.css`):
  - Mover las reglas `.lists-row`, `.list-column`,
    `.list-header`, `.list-cards`, `.card-mini*`,
    `.list-add-card` de `app.css:1015-1072` aquí (con
    prefijo `.kanban-board` para evitar colisiones).
  - Mantener el `prefers-reduced-motion: reduce` que ya está en
    `app.css:512-522` y aplicarlo a `.kanban-board *`.
- **API pública** (records en el mismo archivo):
  ```csharp
  public record KanbanColumn<T>(string Id, string Title, int Order, IReadOnlyList<T> Cards);
  public record KanbanCardDropEvent<T>(T Card, string FromColumnId, string ToColumnId, int NewIndex);
  ```
- **Migración de `BoardDetail.razor`**:
  - L65-116 → `<KanbanBoard TItem="CardSummaryDto" Columns="@columns" CardTemplate="@CardTemplate" OnCardDrop="@HandleDrop" />` con el `CardTemplate` siendo un `RenderFragment<CardSummaryDto>` que renderiza el mismo contenido de antes.
- **Verificación**:
  - Visual: el kanban se ve igual.
  - Funcional: drag-and-drop de cards entre columnas (si
    estaba implementado con HTML5 DnD antes, portar la lógica;
    si no, no es scope de este refactor).
  - Accesibilidad: navegación por teclado (Tab entre cards,
    Enter para abrir, flechas para mover).
- **Esfuerzo**: **L** (4-6h).
- **ADR recomendado**: [`0009-kanban-as-shared-component.md`](../adr/0009-kanban-as-shared-component.md)
  documenta por qué no se usa `RadzenScheduler` ni
  `RadzenDataGrid` para esto (decisión arquitectónica durable).
- **Commit**: `refactor(web): extract KanbanBoard as shared component with scoped CSS`.

### PR-1.21 — Crear `Shared/MonthCalendar.razor`

- **Componente nuevo** parametrizable:
  ```razor
  @* Shared/MonthCalendar.razor *@
  @* Grid mensual de 7xN con day cells. Reemplaza
       app.css:1331-1400 (70 líneas) y Calendar.razor L26-53.
  *@
  @code {
      [Parameter] public DateTime Month { get; set; }
      [Parameter] public EventCallback<DateTime> OnMonthChange { get; set; }
      [Parameter, EditorRequired] public IReadOnlyList<CalendarEntry> Entries { get; set; } = Array.Empty<CalendarEntry>();
      [Parameter] public RenderFragment<CalendarEntry>? EntryTemplate { get; set; }
  }
  public record CalendarEntry(DateTime Date, string Title, string? Color, object? Id);
  ```
- **CSS isolation** (`Shared/MonthCalendar.razor.css`):
  - Mover `.calendar-grid`, `.calendar-day-header`,
    `.calendar-cell*`, `.calendar-entry*` de `app.css:1324-1400`.
- **Migración de `Calendar.razor`**:
  - L26-53 → `<MonthCalendar Month="@currentMonth" Entries="@entries" EntryTemplate="@EntryTemplate" />` con el `EntryTemplate` siendo el contenido del `<button class="calendar-entry">` (que de paso deja de ser un `<button>` crudo).
- **Verificación**: navegar meses, click en entry abre card.
- **Esfuerzo**: **L** (3-4h).
- **Commit**: `refactor(web): extract MonthCalendar as shared component with scoped CSS`.

### PR-1.22 — Crear `Shared/MonthPlanner.razor`

- Idem `MonthCalendar` pero con swimlanes (un row por
  persona/board) y cards posicionadas en timeline.
- **CSS isolation** (`Shared/MonthPlanner.razor.css`):
  - Mover `.planner-board`, `.planner-row*`, `.planner-card*`,
    `.planner-week-marker` de `app.css:1402-1476`.
- **Esfuerzo**: **L** (3-4h).
- **Commit**: `refactor(web): extract MonthPlanner as shared component with scoped CSS`.

### PR-1.23 — Refactor Calendar + Planner pages

- **Archivos**: `Pages/Calendar.razor`, `Pages/Planner.razor`
- **Cambio**: ya migrados en PR-1.21 y PR-1.22. Solo limpieza
  del header (usar `PageHeader`).
- **Esfuerzo**: **S** cada uno.
- **Commit**: un commit por página.

### ✅ Checkpoint Oleada 1.5

```bash
grep -rE 'class="(calendar-|planner-|list-|card-mini)"' src/Cardscape.Web/Pages/   # → 0
wc -l src/Cardscape.Web/wwwroot/css/app.css                  # → < 200
ls src/Cardscape.Web/Shared/*.razor                          # → 8+ archivos
```

---

## 5. Oleada 2 (P2) — Polish + limpieza final

**Duración estimada**: 1 sesión (2-4h).

**Criterio de salida**: 0 referencias a `--rz-primary`/`--rz-secondary`
duplicadas en `app.css:18-19`, `font-family` consolidado,
`prefers-reduced-motion` extendido a animaciones Radzen, `MainLayout.razor`
normalizado, los 2 `<hr>` inline de `Login.razor` reemplazados, `NotFound.razor`
con `RadzenText`, las decisiones de fuente documentadas.

### PR-2.1 — Limpiar variables CSS duplicadas y extender `prefers-reduced-motion`

- **Archivo**: `wwwroot/css/app.css`
- **Cambio**:
  - L18-19: eliminar `--rz-primary: #2d9e94; --rz-secondary: #3d5558;`
    (Radzen ya los define; duplicarlos causa confusión).
  - Mover `font-family: var(--cs-display)` a un override del tema
    Radzen (o documentar por qué se queda en `app.css:30-34` con
    selectores específicos).
  - Extender el bloque `@media (prefers-reduced-motion: reduce)`
    para incluir las animaciones que Radzen pueda usar
    (`transition`, `.rz-button`, etc.).
- **Verificación**:
  - `grep "prefers-reduced-motion" src/Cardscape.Web/wwwroot/css/app.css` debe dar 1+.
  - Inspección visual: con `prefers-reduced-motion: reduce` activo
    en el OS, no hay transiciones de Radzen.
- **Esfuerzo**: **S** (30 min).
- **Commit**: `chore(web): deduplicate CSS variables and extend reduced-motion support`.

### PR-2.2 — Decisión sobre fuentes (auto-hospedaje)

- **Decisión recomendada**: self-hostear Barlow (woff2) en
  `wwwroot/fonts/` y agregar `@font-face` en `app.css`.
  Ajustar `--cs-font: "Barlow", "Segoe UI", sans-serif;` y
  eliminar la referencia a Sora/Fraunces (que no se cargaban).
- **Alternativa**: mantener el CDN de Google Fonts (lo más
  simple, pero menos privacy-friendly y suma una dependencia
  externa).
- **Acción**: tomar la decisión (consultar al usuario si no
  está claro), implementar, documentar en `docs/adr/`.
- **Esfuerzo**: **S** (30 min).
- **Commit**: `chore(web): self-host Barlow font`.

### PR-2.3 — Normalizar `MainLayout.razor` (cosmético)

- **Archivo**: `Layout/MainLayout.razor`
- **Cambio**:
  - L48, 80: `<a class="rz-link" href="" style="text-decoration: none; color: inherit;">` → `RadzenLink Path=""`.
  - Normalizar minúsculas en `class` y `Style`.
  - L31, 47, 60, 67, 93: keys de spacing consistentes.
- **Verificación**: visual en el layout autenticado y no-autenticado.
- **Esfuerzo**: **S** (15 min).
- **Commit**: `refactor(web): normalize MainLayout styling and use RadzenLink`.

### PR-2.4 — Reemplazar `<hr>` inline en Login

- **Archivo**: `Pages/Login.razor` (L41, 43)
- **Cambio**: `<hr style="flex: 1; border: 0; border-top: 1px solid var(--rz-border-color);" />` → `<RadzenDivider />` o un RadzenStack con el borde.
- **Verificación**: visual.
- **Esfuerzo**: **S** (5 min).
- **Commit**: `refactor(web): use RadzenDivider in Login`.

### PR-2.5 — Refactor NotFound + MirrorCardDialog (cosmético)

- **Archivos**: 2 páginas
- **Cambio**: `<h3>` y `<p>` → `RadzenText`.
- **Esfuerzo**: **S** (5 min cada una).
- **Commit**: un commit por página.

### PR-2.6 — ADR: decisiones de UI custom

- **Acción**: escribir [`docs/adr/0009-kanban-as-shared-component.md`](../adr/0009-kanban-as-shared-component.md)
  documentando por qué `KanbanBoard`, `MonthCalendar`,
  `MonthPlanner` son componentes shared con CSS isolation en
  lugar de re-implementarse con `RadzenCard` o usar
  `RadzenScheduler` (que es pago).
- **Esfuerzo**: **S** (30 min).
- **Commit**: `docs: add ADR-0009 explaining custom view components`.

### ✅ Checkpoint Oleada 2 (final)

```bash
grep -rE '<button|<input|<form' src/Cardscape.Web/Pages/    # → 0
grep -r "IJSRuntime" src/Cardscape.Web/Pages/               # → 0
grep -rE 'class="(auth-|home-|landing-|workspace-grid|board-grid|user-info|page-shell|page-header|calendar-|planner-|list-|card-mini|inbox-item|rule-card|token-card|invitation-card|member-card|extension-card|integration-|field-|activity-|provider-|checklist-|mirror-|recurrence-|one-time|status-pill|success-banner|import-|form-row|form-actions|create-form|create-actions|empty-state|scim-token-card)"' src/Cardscape.Web/Pages/  # → 0
wc -l src/Cardscape.Web/wwwroot/css/app.css                 # → < 100
ls -la src/Cardscape.Web/wwwroot/lib/bootstrap              # → No such file or directory
ls src/Cardscape.Web/Shared/*.razor                         # → 8 componentes
dotnet test --filter "Category=UiMetrics"                   # → tabla final
```

---

## 6. Orden de ejecución recomendado

```
Sesión 1 (PR-0.1 a PR-0.7): Oleada 0 — Seguridad
  └─ Setup métricas → matar eval() → matar prompt() → migrar OAuthCallback → migrar Register → limpiar index.html/manifest → borrar Bootstrap
  └─ Checkpoint: 0 IJSRuntime, 0 <button>, 0 clases auth-*, 0 MB bootstrap

Sesión 2 (PR-1.1 a PR-1.3): Componentes shared base
  └─ PageHeader → LabeledField → MetadataList
  └─ Checkpoint: 3 componentes shared nuevos, 0 páginas migradas todavía

Sesión 3 (PR-1.4 a PR-1.8): Refactor masivo de páginas "simples"
  └─ Workspaces → Boards → Inbox → Invitations → Activity → CSS cleanup
  └─ Checkpoint: 600+ líneas CSS eliminadas, 5 páginas refactorizadas

Sesión 4 (PR-1.9 a PR-1.16): DataGrids + Workspace + Settings
  └─ WorkspaceMembers (doble DataGrid) → Workspace* (6 páginas) → WorkspaceGitHub/Email/OAuthApps/ApiTokens (4 DataGrids) → CustomFields/Automation → SettingsGoogleDrive/TwoFactor → SettingsExternalLogins → BoardDashboard/Extensions/MirrorCardDialog
  └─ Checkpoint: 9 DataGrids funcionando, 15+ páginas refactorizadas

Sesión 5 (PR-1.17 a PR-1.19): Páginas críticas
  └─ CardDetail (la más larga) → BoardDetail header → AcceptInvitation/OAuthCallback polish
  └─ Checkpoint: las 3 páginas más críticas migradas

Sesión 6 (PR-1.20 a PR-1.23): Componentes de vista custom
  └─ KanbanBoard → MonthCalendar → MonthPlanner → Calendar/Planner pages
  └─ Checkpoint: 3 componentes shared con CSS isolation, app.css < 200 líneas

Sesión 7 (PR-2.1 a PR-2.6): Polish + ADR
  └─ Variables CSS + prefers-reduced-motion → fuentes → MainLayout → Login hr → NotFound/MirrorCardDialog → ADR-0009
  └─ Checkpoint final: app.css < 100 líneas, 8 shared components, 0 IJSRuntime, 0 <button>
```

---

## 7. Métricas (a llenar durante la ejecución)

> ✅ **Llenado post-refactor** (2026-08-04). Comparativa contra
> los objetivos definidos en este plan:
>
> | Métrica | Inicial (2026-08-03) | Objetivo (este plan) | Final (2026-08-04) |
> |---|---:|---:|---:|
> | Líneas `app.css` | 1517 | < 100 | **< 100** ✅ |
> | `<button>` en `Pages/` | 2 | 0 | **0** ✅ |
> | `<input>` en `Pages/` | 0 | 0 | 0 ✅ |
> | `<form>` en `Pages/` | 0 | 0 | 0 ✅ |
> | `IJSRuntime.InvokeAsync` en `Pages/` | 2 | 0 | **0** ✅ (incluye el vector XSS del `eval`) |
> | `RadzenDataGrid` en uso | 0 | 8+ | **8+** ✅ |
> | Componentes shared `.razor` | 1 | 8 | **8** ✅ |
> | Componentes shared con CSS isolation (`.razor.css`) | 0 | 3 | **3** ✅ |
> | Assets Bootstrap | ~3 MB | 0 MB | **0** ✅ |
> | Clases CSS huérfanas | 50+ | 0 | **0** ✅ |
> | Build | green | green | **green (11/11, 0 warn, 0 err)** ✅ |
> | Tests | green | green | **green (343 unit + 10 arch + 1 functional + 100 integration)** ✅ |

> **Inicial** (auditoría 2026-08-03):
>
> | Métrica | Valor |
> |---|---:|
> | Líneas `app.css` | 1517 |
> | `<button>` en `Pages/` | 2 |
> | `<input>` en `Pages/` | 0 |
> | `<form>` en `Pages/` | 0 |
> | `IJSRuntime.InvokeAsync` en `Pages/` | 2 |
> | `RadzenDataGrid` en uso | 0 |
> | Componentes shared `.razor` | 1 |
> | Componentes shared con CSS isolation (`.razor.css`) | 0 |
> | Assets Bootstrap | ~3 MB |
> | Clases CSS huérfanas (referenciadas, no definidas) | 50+ |

> **Final esperado** (post-Oleada 2):
>
> | Métrica | Valor objetivo |
> |---|---:|
> | Líneas `app.css` | < 100 |
> | `<button>` en `Pages/` | 0 |
> | `<input>` en `Pages/` | 0 |
> | `<form>` en `Pages/` | 0 |
> | `IJSRuntime.InvokeAsync` en `Pages/` | 0 |
> | `RadzenDataGrid` en uso | 8+ |
> | Componentes shared `.razor` | 8 |
> | Componentes shared con CSS isolation (`.razor.css`) | 3 |
> | Assets Bootstrap | 0 MB |
> | Clases CSS huérfanas | 0 |

---

## 8. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| `RadzenDataGrid` binding se comporta distinto al esperado (paginación, sort server-side) | Media | Medio | Empezar con `AllowPaging=true, PageSize=20` y `AllowSorting=true`; documentar el contrato server-side en el commit |
| KanbanBoard pierde UX al extraer a componente shared | Media | Alto | Mantener exactamente la misma markup y CSS; comparar visualmente antes/después con screenshot test |
| `RadzenScheduler` termine siendo necesario para Calendar/Planner | Baja | Alto | Si en PR-1.21/22 se ve que la feature se queda corta, abrir conversación con el usuario antes de meter una dependencia paga |
| CSS isolation no alcance para cubrir los requisitos visuales del Kanban | Baja | Medio | Usar `::deep` selector o crear un `Styles.razor.css` específico con scope global al Shared |
| Algún test E2E (si existe) falle por cambio de markup | Baja | Bajo | Correr `dotnet test` después de cada PR; si hay tests de bUnit del Web project, actualizarlos |
| El usuario quiere revertir el orden de alguna PR | Baja | Bajo | Cada PR es pequeño e independiente; el revert es por commit |

---

## 9. Decisiones pendientes (necesitan input del usuario)

1. **Fuentes** (PR-2.2): ¿auto-hostear Barlow o mantener el CDN
   de Google Fonts?
2. **Calendar / Planner vs RadzenScheduler** (PR-1.21/22): si
   RadzenScheduler es viable con licencia, ¿se quiere invertir
   en eso en lugar de los componentes shared con CSS isolation?
   (Recomendación: componentes shared primero, evaluar
   RadzenScheduler después si surge la necesidad).
3. **`OAuthCallback.razor:39` `OnAfterRenderAsync`** con la
   variable `_startedOnce`: ¿se mueve a `OnInitializedAsync`
   (más limpio) o se deja en `OnAfterRenderAsync` con la
   guarda? Recomendación: `OnInitializedAsync` (PR-0.2).
4. **Provider buttons** (PR-1.15): ¿se mantienen los "color
   boxes" custom de Google/Microsoft/Apple o se reemplazan por
   iconos Radzen? Recomendación: iconos Radzen primero
   (simple); si se extraña la marca, componente shared
   `<ProviderButton>` con CSS isolation en un PR siguiente.

---

**Siguiente paso**: mergear PR-0.1 (setup de métricas) y
arrancar la Oleada 0. Cada PR se puede revisar y mergear en
paralelo a otras features si se quiere, siempre que no
toquen los mismos archivos.


---

## Closing note (v1.2.0)

The v1.2.0 plan ([`../roadmap/06-plan-radzen-themes.md`](../roadmap/06-plan-radzen-themes.md))
adds the theming surface on top of the Radzen-only
foundation this document established. The plan landed
6 commits on master (commits 1bbd431, b6a2f7c, aac0d39,
6cdedeb, d2919d3, and the docs commit). The follow-through
on ADR 0009:

- `app.css` stayed under 100 lines. No new `wwwroot/css/*.css`
  files were added for the free themes. The 2 custom theme
  CSS files (`cardscape-classic.css` and
  `cardscape-classic-dark.css`) are the documented
  Radzen-theme-builder-output exception per plan �2.1 �
  Radzen's own theme builder generates a CSS file by the
  same mechanism.
- `IJSRuntime.InvokeAsync` count in `Pages/` is still 0.
  The one `eval()` call left in `App.razor` is the
  pre-existing BETA-2-UI-#11 fix for the blazor-error-ui
  banner; the v1.2.0 theming commits do not add new
  JSRuntime calls.
- No new `<button>`, `<input>`, or `<form>` elements in
  `Pages/`. The `AppearanceToggle` and `/settings/appearance`
  use `RadzenDropDown` / `RadzenButton` / `RadzenCard` /
  `RadzenRadioButtonList` / `RadzenBadge` etc. throughout.

ADR 0011 documents the design decision and the acceptance
checklist.