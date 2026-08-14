# 01 — Auditoría del estado actual (UI custom vs Radzen)

> **Status**: ✅ **Histórico** (2026-08-04). La auditoría se
> ejecutó el 2026-08-03; el refactor se ejecutó el mismo día
> 2026-08-04 y está terminado. Ver
> [`README.md`](README.md) y [ADR 0009](../adr/0009-radzen-only-ui.md)
> para el estado final.
>
> **Fecha de la auditoría**: 2026-08-03
> **Alcance**: `src/Cardscape.Web/` (Blazor WebAssembly .NET 10
> + `Radzen.Blazor 11.1.8`)
> **Output**: este documento es el inventario exhaustivo de
> todo lo que NO es Radzen en la UI en el momento de la
> auditoría. Es la entrada al [`02-plan.md`](02-plan.md).
>
> **Cero modificaciones** realizadas durante la auditoría —
> solo lectura.

---

## 1. Inventario de assets

### 1.1 CSS

| Archivo | Líneas | Bytes | Propósito | Estado |
|---|---:|---:|---|---|
| `wwwroot/css/app.css` | **1517** | 30 532 | Sistema de diseño "kanban board" + scaffolding de auth/landing + estilos del shell de Blazor | **Usado** (94 referencias custom en 26 `.razor`; mezcla de clases Radzen y custom) |
| `wwwroot/lib/bootstrap/dist/css/bootstrap.css` (+ 15 variantes + `.map`) | — | **~3 MB** | Bootstrap 5 completo | **MUERTO** — no se incluye en `index.html` ni en ningún `.razor`. Residuo de la migración inicial desde Razor Pages. |
| `wwwroot/lib/bootstrap/dist/js/bootstrap.js` (+ 4 variantes + `.map`) | — | **~870 KB** | Bootstrap JS | **MUERTO** |

**Componentes scoped (`.razor.css`)**: **ninguno** en todo
`src/Cardscape.Web/`. No se está aprovechando la feature de
CSS isolation de Blazor en ningún lado.

### 1.2 JS

| Archivo | Estado |
|---|---|
| `wwwroot/service-worker.js` | **Generado por el SDK** (PWA). No tocar. |
| `wwwroot/service-worker-assets.js` | **Generado por el SDK** (manifiesto de assets del SW). No tocar. |
| Cualquier otro `.js` custom | **No existe** como archivo. |
| `IJSRuntime.InvokeAsync` en `.razor` | **2 sitios** (ver §3) |

### 1.3 HTML estático (`wwwroot/index.html`)

| Línea | Contenido | Acción |
|---|---|---|
| 9-11 | `<link>` a Google Fonts (Barlow 100-900 + cursiva) | Mantener o self-hostear. **Decisión pendiente** — ver §6. |
| 12-13 | Radzen CSS (default + material-base) | OK |
| 17 | `<script type="importmap"></script>` **vacío** | **ELIMINAR** |
| 22-28 | `<div id="app">` con SVG spinner de carga de Blazor | Mantener (requerido por el template WASM, depende de `.loading-progress*` en `app.css:102-134`) |
| 30-34 | `<div id="blazor-error-ui">` con clases `dismiss`/`reload` | Mantener (requerido por el template WASM, depende de `#blazor-error-ui` en `app.css:71-90`) |
| 36 | `blazor.webassembly.js` | OK |
| 37-48 | Script inline de registro del Service Worker | OK |

### 1.4 Assets externos

| Recurso | Estado |
|---|---|
| Google Fonts Barlow (`index.html:11`) | Cargado vía CDN. `app.css:16-17` lo nombra como `--cs-font` y `--cs-display`, pero las fuentes referenciadas son `Sora` y `Fraunces` (no se cargan) → fallback a `Segoe UI` / `Georgia`. |
| `fonts/MaterialSymbolsOutlined.woff2` | Lo carga Radzen para los iconos. OK. |
| `fonts/RobotoFlex.woff2` | Lo carga Radzen con `material-base.css`. OK. |
| `fonts/SourceSans3VF-*.woff2` | Lo carga Radzen. OK. |
| `icons/icon-{192,512}.png` + `*-maskable.png` | PWA. OK. |
| `favicon.png` | OK. |
| `lib/bootstrap/**` (~3 MB) | **ELIMINAR** — sin uso. |

---

## 2. Auditoría página por página

> **Leyenda de esfuerzo**: S = <1h · M = 1-3h · L = >3h
> **Leyenda de tipo**: HTML = HTML crudo · CSS = clase custom ·
> JS = `IJSRuntime` · INLINE = estilo inline

### 2.1 Layouts

#### `Layout/MainLayout.razor` ✅ (100 % Radzen)

Sin hallazgos funcionales. Notas cosméticas:

- **L48, 80**: `<a class="rz-link" href="" style="...">` —
  el `<a>` envuelve un `RadzenText`. Reemplazar por
  `RadzenLink Path=""` o eliminar el wrapper.
- **L31, 47, 60, 67, 93**: mezcla `class="rz-..."` y
  `Style="..."` con mayúsculas/minúsculas inconsistentes.
  Normalizar.

Esfuerzo: **S** (cosmético).

#### `Layout/EmptyLayout.razor` ✅

Sin hallazgos.

#### `Layout/RedirectToLogin.razor` ✅

Solo lógica de navegación, no tiene markup.

### 2.2 Shared

#### `Shared/InboxBell.razor` ⚠️ HTML + CSS custom

- **L9-15**: `<a class="inbox-bell" href="inbox">` con
  `<span class="inbox-bell-icon">` (glifo Unicode `\u2407`) y
  `<span class="inbox-bell-badge">` (count).
- **Acción**: `RadzenButton ButtonStyle="Light" Variant="Variant.Text" Icon="notifications" class="rz-border-radius-50"` con `RadzenBadge BadgeStyle="BadgeStyle.Danger" Text="@count.ToString()"` como hijo.
- **CSS a matar** (`app.css:1290-1321`, 37 líneas):
  `.inbox-bell`, `.inbox-bell:hover`, `.inbox-bell-icon`,
  `.inbox-bell-badge`.

Esfuerzo: **S**.

#### `Shared/ApiDtos.cs` y `Shared/RealtimeDtos.cs` ✅

Solo DTOs. Sin hallazgos.

### 2.3 Auth / Landing

#### `Pages/Home.razor` ✅

- **L31, 44, 60**: `Style="font-size: 32px; min-width: 64px; min-height: 64px"` inline en `RadzenIcon`. Trivial.

Esfuerzo: **S**.

#### `Pages/Login.razor` ✅

- **L41, 43**: `<hr style="flex: 1; border: 0; border-top: 1px solid var(--rz-border-color);" />` —
  separador inline. Reemplazar por `<RadzenDivider />` o
  `RadzenStack` con borde.

Esfuerzo: **S**.

#### `Pages/Register.razor` ⚠️ P0 (mezcla Radzen + clases `auth-*`)

- **L14-19**: `<div class="auth-shell"><div class="auth-card"><header class="auth-header"><h1 class="auth-title"><p class="auth-subtitle">` →
  `<RadzenCard class="rz-p-6">` con `RadzenText` (sin header externo porque `EmptyLayout` ya lo provee).
- **L22, 28, 34**: `<div class="auth-field">` con `RadzenLabel` + `RadzenTextBox` separados → `RadzenFormField`.
- **L38**: `<small class="auth-hint">` →
  `RadzenText TextStyle="TextStyle.Caption" class="rz-color-text-secondary"`.
- **L43**: `<div class="auth-error" role="alert">` →
  `RadzenAlert AlertStyle="AlertStyle.Danger" Variant="Variant.Flat" AllowClose="false"`.
- **L46-51**: `RadzenButton class="auth-submit"` — `auth-submit` solo es `width: 100%` → mover a `Style="width:100%"` o `class="rz-w-100"`.
- **L54-56**: `<p class="auth-footer">...<a href="login">` →
  `RadzenStack` + `RadzenLink Path="login"`.

Esfuerzo: **M**. Mata de `app.css:291-397` (107 líneas):
`.auth-shell`, `.auth-card`, `.auth-header`, `.auth-title`,
`.auth-subtitle`, `.auth-form`, `.auth-field`,
`.auth-field .rz-label`, `.auth-submit`, `.auth-back-link`,
`.auth-error`, `.auth-hint`, `.auth-footer`,
`.auth-footer a`, `.auth-footer a:hover`.

#### `Pages/OAuthCallback.razor` ⚠️ P0 (HTML + CSS + JS)

- **L17-19, 25-27, 31**: usa `.auth-shell`, `.auth-card--status`,
  `.provider-button`, `.auth-spinner` — todas definidas en
  `app.css:283-490` y **ya no necesarias** porque
  `EmptyLayout.razor` provee el card.
- **L25**: `<a class="provider-button" href="login" style="justify-content: center;">` →
  `<RadzenButton Text="@L["AuthBackToSignIn"]" ButtonStyle="ButtonStyle.Primary" Click="@(() => Nav.NavigateTo("login"))" />`.
- **L31**: `<div class="auth-spinner" aria-hidden="true"></div>` →
  `<RadzenProgressBarCircular Mode="ProgressBarMode.Indeterminate" Size="ProgressBarCircularSize.Medium" />` o
  `<RadzenProgressBar Mode="ProgressBarMode.Indeterminate" />`.
- **L39 `OnAfterRenderAsync` + L48 `JS.InvokeAsync<string>("eval", "window.location.href")`** —
  ⚠️ **ANTI-PATRÓN DE SEGURIDAD**. `eval` ejecuta JS arbitrario:
  vector XSS si el fragmento del hash viene comprometido. Migrar
  a:
  ```csharp
  var uri = new Uri(Nav.Uri);
  string fragment = uri.Fragment.TrimStart('#');
  ```
  El `Nav` ya está inyectado en otras páginas (vía `@inject NavigationManager Nav`).
  Pero L39 también usa `OnAfterRenderAsync` con una variable estática
  `_startedOnce` que solo se usa para evitar múltiples registros. Eso se
  puede mover a un `await Task.Yield(); StateHasChanged();` o reemplazar
  por un `OnInitializedAsync` que haga el redirect directo (sin pasar
  por JS).

Esfuerzo: **S** mecánico. Gran victoria de seguridad.

#### `Pages/NotFound.razor` ⚠️ P2

- `<h3>` y `<p>` crudos → `RadzenText`.

Esfuerzo: **S**.

#### `Pages/AcceptInvitation.razor` ⚠️ P1

- **L11-22, 38-48**: `<div class="page-shell"><div class="page-header"><h1>` →
  usar el componente shared `PageHeader` (a crear).
- **L18**: `<p class="auth-error" role="alert">` → `RadzenAlert`.
- **L42**: `<p class="muted">` →
  `RadzenText TextStyle="TextStyle.Body2" class="rz-color-text-secondary"`.

Esfuerzo: **M**.

### 2.4 Workspaces

#### `Pages/Workspaces.razor` ⚠️ P1

- **L13-15**: header `page-shell` + `page-header` → `PageHeader`.
- **L34**: `<div class="auth-error" role="alert">` → `RadzenAlert`.
- **L55-71**: `<div class="workspace-grid">` con `@foreach` de
  `RadzenCard` → `RadzenRow Gap="1rem"` +
  `RadzenColumn Size="12" SizeMD="6" SizeLG="4"`.

Esfuerzo: **M**. Mata de `app.css:911-929` (19 líneas):
`.workspace-grid`, `.workspace-card`, `.workspace-card h3`.

#### `Pages/WorkspaceMembers.razor` ⚠️ P1 (DataGrid + RadzenAlert)

- **L13-23, 37-60, 74-87, 100-115**: cuatro `div.member-list` /
  `.invitation-list` con cards.
- **L39-48**: `<div class="issued-secret">` con `<h3>` y
  `<pre class="secret-box">` → `RadzenAlert AlertStyle="AlertStyle.Warning" Variant="Variant.Flat" AllowClose="false"` con `<code>` adentro.
- **L52, 67**: `<div class="auth-error">` → `RadzenAlert`.
- **L74-86**: `<div class="member-list">` con RadzenCards →
  `RadzenDataGrid TItem=WorkspaceMemberDto` con sort/filter/paginate gratis.
- **L100-115**: `<div class="invitation-list">` con RadzenCards →
  `RadzenDataGrid TItem=WorkspaceInvitationDto`.

Esfuerzo: **L** (dos DataGrids + reorganización). Mata de
`app.css:1192-1229` (38 líneas): `.invitation-list`,
`.member-list`, `.invitation-card`, `.member-card`,
`.invitation-row`, `.member-row`, `.invitation-meta`,
`.invitation-actions`.

#### `Pages/WorkspaceIntegrations.razor` ⚠️ P1

- **L9-14**: header `page-shell` + `page-header` → `PageHeader`.
- **L15-64**: `<div class="integration-grid">` (clase huérfana) → `RadzenRow` + `RadzenColumn` con `<RadzenCard Variant="Variant.Outlined">`.

Esfuerzo: **M**.

#### `Pages/WorkspaceSaml.razor` ⚠️ P1

- **L11-15**: header `page-shell` + `page-header` → `PageHeader`.
- **L26-35**: 5 `RadzenLabel` con `Style="margin-top:1rem; display:block"` —
  patrón repetido en 7+ páginas. Crear `LabeledField` shared
  (label + control con spacing consistente) o usar
  siempre `RadzenFormField`.
- **L36-39**: `<div class="create-actions">` con un solo
  `RadzenButton` — el wrapper es innecesario.

Esfuerzo: **M**.

#### `Pages/WorkspaceScim.razor` ⚠️ P1

- **L11-16**: header `page-shell` + `page-header` → `PageHeader`.
- **L51-64**: `<RadzenCard class="scim-token-card">` (clase huérfana) → `Variant="Variant.Outlined"`.

Esfuerzo: **M**.

#### `Pages/WorkspaceSlack.razor` ⚠️ P1

Mismo patrón que WorkspaceSaml.

Esfuerzo: **M**.

#### `Pages/WorkspaceGitHub.razor` ⚠️ P1 (DataGrid)

- **L22-31**: 3 `RadzenLabel` con patrón repetido → `LabeledField`.
- **L41-45**: `<div class="form-row">` (huérfana) → `RadzenFormField`.
- **L65-91**: `<table class="cards-table">` con PRs de GitHub →
  `RadzenDataGrid TItem=GitHubPullRequestDto` con columnas `Number`, `Title`, `State`, action.
- **L96-109**: form con 4 `RadzenLabel` → `RadzenFormField`.
- **L110-123**: `<div class="success-banner">` → `RadzenAlert AlertStyle="AlertStyle.Success"`.

Esfuerzo: **L** (DataGrid + form refactor).

#### `Pages/WorkspaceEmail.razor` ⚠️ P1 (DataGrid)

- **L29-34**: 3 `RadzenLabel` → `RadzenFormField`.
- **L52-78**: `<table class="cards-table">` con email addresses →
  `RadzenDataGrid TItem=InboundEmailAddressDto`.

Esfuerzo: **L** (DataGrid).

#### `Pages/WorkspaceImport.razor` ⚠️ P1

- **L12-13**: `<div class="page-shell"><h1>` sin header completo → `PageHeader`.
- **L22, 26**: `<p class="import-status">` (huérfana) → `RadzenProgressBar Indeterminate`.
- **L30-32**: `<div class="auth-error">` → `RadzenAlert`.
- **L80-89**: `<div class="form-actions">` (huérfana) → `RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End" Gap="0.5rem"`.

Esfuerzo: **M**.

### 2.5 Boards / Cards

#### `Pages/Boards.razor` ⚠️ P1

- **L10-15**: header `page-shell` + `page-header` → `PageHeader`.
- **L36**: `<div class="auth-error">` → `RadzenAlert`.
- **L57-73**: `<div class="board-grid">` con RadzenCards → `RadzenRow` + `RadzenColumn`.

Esfuerzo: **M**. Mata de `app.css:931-949` (19 líneas):
`.board-grid`, `.board-card`, `.board-card h3`.

#### `Pages/BoardDetail.razor` ⚠️ P1 (Kanban — la vista más custom)

- **L16-50**: header + description + actions.
- **L26-29**: `<span class="live-indicator">` con `<span class="dot">` →
  `RadzenBadge BadgeStyle="BadgeStyle.Success" Text="● Live"`
  o `BadgeStyle="BadgeStyle.Light" Text="○ Offline"`.
- **L36-39**: `<p class="board-description">` → `RadzenText TextStyle="TextStyle.Body2" class="rz-color-text-secondary"`.
- **L54-62**: `<RadzenCard class="add-list-form">` → `RadzenCard Variant="Variant.Flat"`.
- **L65-116**: **el kanban real** — `<div class="lists-row">` con scroll horizontal, `<div class="list-column">`, `<header class="list-header">`, `<div class="list-cards">`, `<div class="card-mini">`, `<div class="list-add-card">`. **El widget más custom del proyecto**.
  - **Decisión**: crear `Shared/KanbanBoard.razor` (componente
    parametrizable con `TItem` y slots para header/footer) y
    mover el CSS a `Shared/KanbanBoard.razor.css` con **CSS
    isolation de Blazor**. La vista Kanban no tiene
    equivalente Radzen directo y re-implementarla con
    `RadzenCard` sacrifica UX.
  - Alternativa descartada: `RadzenDataGrid` (pierde la
    metáfora visual de columnas con scroll horizontal).

Esfuerzo: **L** (kanban como componente shared + CSS
isolation + extracción de responsabilidades). Mata de
`app.css:951-1072` (122 líneas): `.board-shell`,
`.board-header*`, `.live-indicator*`, `.board-description`,
`.board-actions`, `.add-list-form`, `.lists-row`,
`.list-column`, `.list-header`, `.list-cards`, `.card-mini*`,
`.list-add-card`.

#### `Pages/BoardDashboard.razor` ⚠️ P1

- **L12-17**: header `page-shell` + `page-header` → `PageHeader`.
- **L21-36**: `<RadzenCard class="create-form">` → `RadzenCard Variant="Variant.Flat"`.
- **L24, 26, 28**: 3 `RadzenLabel` con `Style="margin-top:1rem; display:block"` → `LabeledField`.
- **L49-60**: `<div class="dashboard-grid">` (huérfana) → `RadzenRow` + `RadzenColumn`.
- **L63-66**: `<div class="auth-error">` → `RadzenAlert`.

Esfuerzo: **M**.

#### `Pages/BoardExtensions.razor` ⚠️ P1

- **L14-21**: header `page-shell` + `page-header` → `PageHeader`.
- **L34**: `<RadzenCard class="@(isEnabled ? "extension-card" : "extension-card extension-card--off")">` →
  `Variant="@(isEnabled ? Variant.Filled : Variant.Outlined)"` + `class="rz-p-3"`.
- **L35-40**: `<div class="extension-row">` + `<div class="extension-meta">` (huérfanas) →
  `RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.SpaceBetween" Gap="1rem"`.

Esfuerzo: **M**.

#### `Pages/CardDetail.razor` ⚠️ P1 (página más larga)

- **L24-32**: `<div class="card-detail-shell">` con `RadzenCard` → `class="rz-mx-auto"` + `Style="max-width:48rem"`.
- **L32-34**: `<div class="card-detail-header">` con `h1` + acciones → `RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.SpaceBetween" Gap="1rem"`.
- **L33**: `<h1>` crudo → `RadzenText TextStyle="TextStyle.H5" TagName="TagName.H1"`.
- **L47-76**: `<div class="card-detail-actions">` con botones + `<small class="vote-count">` → `RadzenBadge` o `RadzenText`.
- **L79-101**: `<dl class="card-meta">` con `dt`/`dd` (grid `8rem 1fr`) → componente shared `<MetadataList>` o `RadzenRow` + 2 `RadzenColumn` (label + value).
- **L130, 133**: `style="margin:.5rem 0 0 0; padding-left:1rem"` → clases Radzen `rz-mt-2 rz-pl-4`.
- **L179**: `<div class="ai-actions" style="margin-bottom:.5rem">` → `RadzenStack` con `Gap="0.5rem"`.
- **L274-294**: `<ul class="card-activity-list">` → `RadzenTimeline` o `RadzenDataGrid`.
- **L107-123**: `<div class="card-snooze-section">` (huérfana) → `RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="0.5rem"`.
- **L235-271**: `.checklist-*` (huérfanas) → `RadzenCard` + `RadzenStack` patterns.

Esfuerzo: **L** (página más larga, muchos patrones).

#### `Pages/MirrorCardDialog.razor` ⚠️ P1

- **L22**: `<div class="mirror-card-dialog">` (huérfana) → `RadzenStack Gap="1rem"`.
- **L23-24**: `<h3>` + `<p>` → `RadzenText`.
- **L55-61**: `<div class="mirror-card-actions">` (huérfana) → `RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End" Gap="0.5rem"`.

Esfuerzo: **S**.

### 2.6 Productivity

#### `Pages/Calendar.razor` ⚠️ P1 (Calendar mensual custom)

- **L11-19**: header `page-shell` + `page-header` con botones de mes → `PageHeader` + acciones inline.
- **L16**: `<strong class="month-label">` → `RadzenText TextStyle="TextStyle.H6"`.
- **L26-53**: **el grid del calendario mensual** es custom (CSS `app.css:1331-1400`, 70 líneas).
  - **Decisión**: extraer a `Shared/MonthCalendar.razor` con CSS isolation (`.razor.css`). La metáfora de mes/semana/día-cell no la provee `RadzenScheduler` (que es pago y trae otra UX).
- **L45-49**: `<button class="calendar-entry">` — **el único `<button>` crudo** que queda en `Pages/`. Reemplazar por `RadzenButton ButtonStyle="ButtonStyle.Light" Variant="Variant.Text" Size="ButtonSize.ExtraSmall" class="rz-p-1"`.

Esfuerzo: **L** (gran refactor visual).

#### `Pages/Planner.razor` ⚠️ P1 (Swimlanes custom)

Mismo patrón que Calendar. CSS `app.css:1402-1476` (74 líneas).

- **L51-56**: `<button class="planner-card">` — segundo `<button>` crudo de `Pages/`. Mismo reemplazo.

Esfuerzo: **L**.

#### `Pages/Inbox.razor` ⚠️ P1

- **L12-25**: header → `PageHeader`.
- **L46-72**: `<div class="inbox-list">` con `RadzenCard class="inbox-item inbox-item--unread/read"` → `RadzenCard Variant="Variant.Flat"` con `Style="border-left: 3px solid var(--rz-info);"` o `RadzenDataGrid TItem=NotificationDto`.
- **L74-77**: `<div class="auth-error">` → `RadzenAlert`.

Esfuerzo: **M**. Mata de `app.css:1242-1288` (47 líneas):
`.inbox-list`, `.inbox-item*`, `.inbox-row`, `.inbox-meta*`,
`.inbox-actions`.

#### `Pages/Invitations.razor` ⚠️ P1

- **L11-15**: header → `PageHeader`.
- **L27-43**: `<div class="invitation-list">` con RadzenCards → `RadzenDataGrid`.
- **L44-48**: `<p class="muted">` → `RadzenText` secondary.
- **L51-54**: `<div class="auth-error">` → `RadzenAlert`.

Esfuerzo: **M**.

#### `Pages/Activity.razor` ⚠️ P1

- **L12-19**: header → `PageHeader`.
- **L31-54**: `<div class="activity-list">` con RadzenCards (clases huérfanas) → `RadzenDataGrid` o `RadzenCard` + `RadzenStack`.
- **L44**: `<a href="@($"cards/{cardId}")">@cardId.ToString()[..8]</a>` → `RadzenLink Path=...`.
- **L46**: `<small>` crudo → `RadzenText`.
- **L58-62**: `<div class="activity-loadmore">` (huérfana) → `RadzenStack` con `JustifyContent="JustifyContent.Center"`.
- **L65-68**: `<div class="auth-error">` → `RadzenAlert`.

Esfuerzo: **M**.

### 2.7 Automation

#### `Pages/Automation.razor` ⚠️ P1

- **L10-17**: header → `PageHeader`.
- **L25-66**: `<RadzenCard class="create-form">` con form → `RadzenCard Variant="Variant.Flat"`.
- **L29, 32, 45, 50**: 4 `RadzenLabel` con `Style="margin-top:.5rem;display:block"` → `LabeledField`.
- **L55-58**: `<div class="auth-error">` → `RadzenAlert`.
- **L78-115**: `<div class="rule-list">` con RadzenCards (CSS `app.css:1478-1516`, 38 líneas) → `RadzenDataGrid` o `RadzenCard Variant="Variant.Outlined"` con `Style="border-left: 3px solid var(--rz-success);"`.

Esfuerzo: **M**.

#### `Pages/CustomFields.razor` ⚠️ P1 (DataGrid + Form)

- **L12-17**: header → `PageHeader`.
- **L31**: `<p class="empty-state">` (huérfana) → `RadzenText` o `RadzenAlert AlertStyle="AlertStyle.Secondary"`.
- **L33-69**: `<div class="field-list">` con RadzenCards → `RadzenDataGrid TItem=CustomFieldDefinitionDto`.
- **L71-98**: form con `<div class="form-row">` (huérfana, 3 veces) → `RadzenFormField`.
- **L92-96**: `<div class="form-actions">` (huérfana) → `RadzenStack` horizontal.
- **L100-103**: `<div class="auth-error">` → `RadzenAlert`.

Esfuerzo: **L** (DataGrid + form refactor).

### 2.8 Settings

#### `Pages/SettingsExternalLogins.razor` ⚠️ P1

- **L19-32**: `<div class="provider-buttons">` con 3 `<a class="provider-button">` (CSS `app.css:405-435`) → 3 `RadzenButton ButtonStyle="ButtonStyle.Light" Icon="..."` (Google → `cloud_circle`, Microsoft → `phonelink`, Apple → `phone_iphone`).
  - Alternativa: si se quieren los "color boxes" de cada provider, mantener `.provider-google/microsoft/apple` con CSS isolation de Blazor en un componente shared `<ProviderButton>`.

Esfuerzo: **M**.

#### `Pages/SettingsTwoFactor.razor` ⚠️ P0 (seguridad JS) + P1

- **L80 `OnAfterRenderAsync` + L131 `JS.InvokeAsync<string>("prompt", ...)`** —
  ⚠️ prompt nativo del navegador (UX horrible, accesible pero
  no-Blazor). Reemplazar por `DialogService.OpenAsync` con un
  `RadzenTextBox` adentro (sigue al patrón de los otros dialogs
  del proyecto).
- **L11-12**: `<div class="page-shell"><h1>` sin header completo → `PageHeader`.
- **L20-30, 34-55, 59-65**: tres RadzenCards con `<h2>` y `<h3>` crudos → `RadzenText TextStyle="TextStyle.H5"`.
- **L37-40**: `<a href="@enrollment.QrCodeUrl" target="_blank" rel="noopener">` → `RadzenLink Target="_blank"`.
- **L70**: `<div class="auth-error" role="alert" style="margin-top:1rem">` → `RadzenAlert AllowClose="false"`.

Esfuerzo: **M**.

#### `Pages/SettingsGoogleDrive.razor` ⚠️ P1

- **L13-17**: header → `PageHeader`.
- **L21-22**: `RadzenLabel` + `RadzenDropDown` separados → `RadzenFormField`.
- **L42-44**: `<div class="auth-error">` → `RadzenAlert`.

Esfuerzo: **S**.

#### `Pages/SettingsOAuthApps.razor` ⚠️ P1 (DataGrid)

- **L10-14**: header → `PageHeader`.
- **L23-28, 39-58**: `<ul>`, `<table class="oauth-apps-table">` (3 veces `<div class="form-row">`).
- **L40-55**: 3 `RadzenLabel` con `Component` y luego un `RadzenTextBox` con `Name` → `RadzenFormField`.
- **L62-74**: `<div class="one-time-secret">` (huérfana) → `RadzenAlert AlertStyle="AlertStyle.Warning"`.
- **L89-127**: **`<table class="oauth-apps-table">` con OAuth apps** → `RadzenDataGrid TItem=OAuthAppSummaryDto` con columna `Status` que sea `RadzenBadge` condicional + columna `Actions` con revoke.

Esfuerzo: **L** (DataGrid).

#### `Pages/ApiTokens.razor` ⚠️ P1 (DataGrid + Form)

- **L10-15**: header → `PageHeader`.
- **L31-37**: `<div class="scopes">` con CheckBox + Label inline (no usa FormField).
- **L33, 35**: `RadzenCheckBox` con `Name` y label separado → `RadzenFormField`.
- **L39-47**: 2 `RadzenLabel` + 2 `RadzenNumeric` + `<small>` → `RadzenFormField` con `Help` parameter.
- **L49-52**: `<div class="auth-error">` → `RadzenAlert`.
- **L62-73**: `<RadzenCard class="issued-secret">` con `<h3>`, `<pre class="secret-box">` → `RadzenAlert` con el secret como `<code>`.
- **L85-137**: `<div class="token-list">` con RadzenCards (CSS `app.css:1160-1190`, 31 líneas) → `RadzenDataGrid TItem=ApiTokenSummaryDto`.

Esfuerzo: **L** (DataGrid + form refactor).

---

## 3. Inventario de `IJSRuntime` (los 2 sitios)

| Archivo:línea | Código actual | Riesgo | Migración |
|---|---|---|---|
| `Pages/OAuthCallback.razor:48` | `JS.InvokeAsync<string>("eval", "window.location.href")` | **XSS vector** — `eval` ejecuta JS arbitrario del hash | Usar `NavigationManager.Uri` + parsear el fragmento (ver §2.3) |
| `Pages/SettingsTwoFactor.razor:131` | `JS.InvokeAsync<string>("prompt", "...")` | UX rota, no es un Blazor dialog | `DialogService.OpenAsync` con `RadzenTextBox` (ver §2.8) |

**Conteo de grep**:
- `<button` en `Pages/`: **2** (`Calendar.razor:45`, `Planner.razor:51`)
- `<input` en `Pages/`: **0**
- `<form` en `Pages/`: **0**
- `<a href` en `Pages/`: 6 (todos links externos OAuth/docs, no navegación interna)
- `<table` en `Pages/`: 3 (`SettingsOAuthApps.razor:89`, `WorkspaceEmail.razor:53`, `WorkspaceGitHub.razor:65`)
- `IJSRuntime`: 2 (ya listados arriba)
- `OnAfterRenderAsync`: 2 (los mismos 2)
- `RadzenTemplateForm`: 34 ocurrencias en 15 archivos (uso correcto)
- `EditForm`: **0**
- `RadzenDataGrid`: **0** (oportunidad enorme en las 5 listas largas y 3 tablas)

---

## 4. CSS custom en `app.css` (mapa de eliminación)

### 4.1 Lo que se queda (4 elementos, todos del template WASM)

| Líneas | Contenido | Por qué se queda |
|---|---|---|
| 1-20 | `:root { --cs-* }` | Tokens de tema custom que Radzen no provee. **MANTENER**, pero limpiar duplicados (`--rz-primary`, `--rz-secondary` están duplicados — Radzen ya los define). |
| 22-28 | `html, body { font-family: var(--cs-font); ... }` | Override global de tipografía. MANTENER o pasar a override de tema Radzen. |
| 71-90 | `#blazor-error-ui` y `.dismiss` | Overlay estándar de error de Blazor (`index.html:30`). MANTENER. |
| 92-100 | `.blazor-error-boundary` | Boundary estándar de error de Blazor. MANTENER. |
| 102-134 | `.loading-progress` + `.loading-progress-text` | Spinner SVG inicial antes del primer render (`index.html:23-28`). MANTENER. |
| 136-138 | `code { color: #c02d76 }` | MANTENER (tema custom) o pasar a override de tema. |
| 512-522 | `@media (prefers-reduced-motion: reduce) { ... }` | MANTENER y extender a las animaciones Radzen. |

### 4.2 Lo que se va (todo lo demás)

| Líneas (en `app.css`) | Bloque | Acción |
|---|---|---|
| 30-34 | `font-family: var(--cs-display)` en h1-h3 | MOVER a override de tema Radzen o CSS isolation global en `_Layout` |
| 36-38 | `h1:focus { outline: none }` | MOVER al MainLayout como CSS isolation |
| 40-42 | `a, .btn-link { color: var(--cs-accent-strong) }` | MOVER a override de tema |
| 44-69 | `.btn-primary`, `.btn:focus`, `.valid.modified`, `.invalid`, `.validation-message` | **ELIMINAR** — Bootstrap remnants sin uso |
| 140-147 | `.form-floating` | **ELIMINAR** — Bootstrap remnant |
| 149-183 | `.brand-lanes`, `.brand-wordmark` | MOVER a `MainLayout.razor.css` (es el logo custom del sidebar) |
| 185-281 | `.auth-page`, `.auth-stage`, `.auth-brand*` | **ELIMINAR** — ya reemplazado por `EmptyLayout` + `RadzenCard` |
| 283-289 | `.auth-main` | **ELIMINAR** |
| 291-306 | `.auth-shell`, `.auth-card`, `.auth-card--status` | **ELIMINAR** |
| 308-323 | `.auth-header`, `.auth-title`, `.auth-subtitle` | **ELIMINAR** — `RadzenText` |
| 325-329 | `.auth-form` | **ELIMINAR** — `RadzenStack` |
| 331-341 | `.auth-field`, `.auth-field .rz-label` | **ELIMINAR** — `RadzenFormField` |
| 343-346 | `.auth-submit` | **ELIMINAR** — `Style="width:100%"` |
| 348-364 | `.auth-back-link` | **ELIMINAR** — `RadzenButton` |
| 366-373 | `.auth-error` | **ELIMINAR** — `RadzenAlert` |
| 375-380 | `.auth-hint` | **ELIMINAR** — `RadzenText` |
| 382-397 | `.auth-footer`, `.auth-footer a` | **ELIMINAR** — `RadzenStack` + `RadzenLink` |
| 399-452 | `.auth-providers`, `.provider-button`, `.provider-glyph`, `.provider-google/microsoft/apple` | **ELIMINAR** — `RadzenButton` |
| 454-471 | `.auth-divider` | **ELIMINAR** — `RadzenDivider` |
| 473-490 | `.auth-spinner` | **ELIMINAR** — `RadzenProgressBar` indeterminate |
| 483-490 | `@keyframes cs-rise`, `@keyframes cs-spin` | **ELIMINAR** si no se usan en otro lado |
| 492-510 | `@media (max-width: 860px)` con `.auth-*` | **ELIMINAR** |
| 524-547 | `.user-info`, `.user-name`, `.user-email`, `.top-link` | **ELIMINAR** — huérfanas |
| 549-564 | `.page-shell`, `.page-header`, `.page-header h1` | **CONVERTIR** en componente shared `PageHeader` |
| 566-665 | `.home-shell*`, `.home-eyebrow`, `.home-hero*`, `.home-lead`, `.home-section*`, `.home-count`, `.home-empty`, `.home-board-rail`, `.home-board-tile*`, `.home-workspace-*`, `.home-shortcuts*` | **ELIMINAR** — `Home.razor` ya está migrado a Radzen |
| 764-896 | `.landing-*` (~130 líneas) | **ELIMINAR** — muerto, `Home.razor` ya no usa |
| 898-909 | `.create-form`, `.create-actions` | **CONVERTIR** en patrón RadzenCard + RadzenStack |
| 911-929 | `.workspace-grid`, `.workspace-card*` | **ELIMINAR** — `RadzenRow` + `RadzenColumn` |
| 931-949 | `.board-grid`, `.board-card*` | **ELIMINAR** — `RadzenRow` + `RadzenColumn` |
| 951-1008 | `.board-shell`, `.board-header*`, `.live-indicator*` | **PARCIAL**: `.board-shell`/`.board-header` → `PageHeader`; `.live-indicator*` → `RadzenBadge` |
| 1010-1072 | `.add-list-form`, `.lists-row`, `.list-column`, `.list-header`, `.list-cards`, `.card-mini*`, `.list-add-card` | **MOVER** a `Shared/KanbanBoard.razor.css` |
| 1074-1122 | `.card-detail-shell`, `.card-detail-header*`, `.card-detail-actions`, `.card-meta*`, `.card-description`, `.comment-card` | **CONVERTIR** en `MetadataList` shared o `RadzenStack` + `RadzenText` |
| 1124-1190 | `.page-hint`, `.scopes`, `.issued-secret*`, `.secret-box`, `.token-list`, `.token-card`, `.token-row*`, `.token-actions` | **ELIMINAR** — `RadzenDataGrid` + `RadzenAlert` |
| 1192-1229 | `.invitation-list`, `.member-list`, `.invitation-card*`, `.member-card*`, `.invitation-row*`, `.invitation-actions` | **ELIMINAR** — `RadzenDataGrid` |
| 1231-1240 | `.page-header-actions`, `.muted` | **ELIMINAR** — `RadzenStack` + `RadzenText` secondary |
| 1242-1288 | `.inbox-list`, `.inbox-item*`, `.inbox-row`, `.inbox-meta*`, `.inbox-actions` | **ELIMINAR** — `RadzenDataGrid` o `RadzenCard` + `Style` |
| 1290-1321 | `.inbox-bell*` | **ELIMINAR** — `RadzenButton` + `RadzenBadge` |
| 1324-1400 | `.month-label`, `.calendar-grid`, `.calendar-day-header`, `.calendar-cell*`, `.calendar-entry*` | **MOVER** a `Shared/MonthCalendar.razor.css` |
| 1403-1476 | `.planner-board`, `.planner-row*`, `.planner-card*`, `.planner-week-marker` | **MOVER** a `Shared/MonthPlanner.razor.css` |
| 1478-1516 | `.rule-list`, `.rule-card*`, `.rule-row`, `.rule-meta*`, `.rule-actions` | **ELIMINAR** — `RadzenDataGrid` |

### 4.3 Clases huérfanas (referenciadas en `.razor` pero NO en `app.css`)

Estas clases se usan en markup pero **no existen en `app.css`**. Resultado de limpiezas parciales previas:

```
.activity-card, .activity-row, .activity-meta, .activity-payload, .activity-loadmore
.ai-actions, .ai-preview, .ai-suggestions, .ai-summary
.cards-table
.checklist-card, .checklist-header, .checklist-item, .checklist-item-done, .checklist-add-item, .checklist-create
.dashboard-grid, .dashcard
.empty-state
.extension-card, .extension-card--off, .extension-row, .extension-meta, .extension-actions, .extension-config, .extension-list
.field-card, .field-kind-badge, .field-list, .field-meta, .field-actions, .field-row
.form-actions, .form-row
.import-preview-panel, .import-result-panel, .import-status
.integration-card
.mirror-card-actions, .mirror-card-blurb, .mirror-card-dialog, .mirror-card-title
.one-time-secret
.oauth-apps-table, .status-pill, .status-pill.active, .status-pill.revoked
.rate-limit
.recurrence-create
.scim-token-card
.success-banner
.vote-count
```

**Acción**: confirmar en el DOM que se renderizan sin estilo
(no se aplican reglas) y reemplazar por `Radzen*` nativos en
los PRs de P1.

---

## 5. Dependencias y configuración

### 5.1 `Cardscape.Web.csproj`

✅ **Nada que eliminar del csproj**. `Radzen.Blazor 11.1.8` ya está
declarado. No hay paquete Bootstrap residual.

### 5.2 `wwwroot/manifest.webmanifest`

**Inconsistencia detectada** (línea 9): `theme_color: #1d4ed8`
(azul) no coincide con `index.html:16` `theme-color: #0f3d3e`
(verde-azulado oscuro que coincide con `--cs-canvas`).

**Acción**: alinear `theme_color` con `index.html:16` y
opcionalmente `background_color` para que coincida con el
default body background.

### 5.3 Assets huérfanos a eliminar

```
wwwroot/lib/bootstrap/dist/css/bootstrap.css                  281 KB
wwwroot/lib/bootstrap/dist/css/bootstrap.min.css              233 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-grid.css              70 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-grid.min.css          52 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.css            12 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.min.css        10 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.css        108 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.min.css     85 KB
wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.css              280 KB
wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.min.css          233 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-grid.rtl.css          70 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.rtl.css        12 KB
wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.rtl.css    107 KB
wwwroot/lib/bootstrap/dist/js/bootstrap.js                    145 KB
wwwroot/lib/bootstrap/dist/js/bootstrap.min.js                 61 KB
wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.js             208 KB
wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js          81 KB
wwwroot/lib/bootstrap/dist/js/bootstrap.esm.js                135 KB
wwwroot/lib/bootstrap/dist/js/bootstrap.esm.min.js             74 KB
+ todos los .map (~3.5 MB más)
+ directorio wwwroot/lib/bootstrap/ completo (incluye LICENSE/README si los hay)
```

No hay referencias a estos archivos en `index.html`, `Program.cs`,
ni en ningún `.razor`. Verificado con grep recursivo.

**Total eliminable**: **~3 MB**.

---

## 6. Cosas que NO se pueden migrar a Radzen (decisión explícita)

1. **Google Fonts (Barlow)** en `index.html:9-11` — fuente
   externa. Mantener o self-hostear. **Decisión recomendada**:
   self-hostear Barlow (woff2) en `wwwroot/fonts/` y agregar
   `@font-face` en `app.css`. La razón: el `app.css` actual
   referencia `Sora` y `Fraunces` que **nunca se cargan** —
   la fuente real es solo Barlow. Decisión: usar Barlow
   (auto-hospedada) o ajustar `--cs-font` a la realidad.

2. **Spinner SVG inicial** en `index.html:23-28` +
   `.loading-progress*` (`app.css:102-134`) — es el spinner
   que muestra Blazor antes del primer render. El template
   WASM lo requiere. MANTENER.

3. **`#blazor-error-ui`** en `index.html:30-34` +
   `app.css:71-90` — overlay de error de Blazor. MANTENER.

4. **`#blazor-error-boundary`** + `app.css:92-100` — boundary
   de error de Blazor. MANTENER.

5. **Service Worker** registration en `index.html:37-48` y
   `wwwroot/service-worker.js` — PWA estándar. MANTENER.

6. **Vistas Kanban / Calendar mensual / Planner mensual** —
   no tienen equivalente Radzen directo fuera de
   `RadzenScheduler` (que es pago en algunas versiones y trae
   UX distinta). **Decisión**: extraer a componentes
   `Shared/KanbanBoard.razor`, `Shared/MonthCalendar.razor`,
   `Shared/MonthPlanner.razor` con **CSS isolation de Blazor**
   (`.razor.css`). Si en el futuro se quiere drag-drop o
   time-grid real, evaluar `RadzenScheduler` con licencia
   comercial (queda como ADR pendiente).

7. **`@media (prefers-reduced-motion: reduce)`** en
   `app.css:512-522` — accesibilidad. MANTENER y extender.

---

## 7. Resumen numérico

| Métrica | Valor |
|---|---:|
| Páginas `.razor` en `Pages/` | 33 |
| Layouts `.razor` | 3 |
| Componentes shared `.razor` | 1 (`InboxBell`) |
| Líneas de CSS custom en `app.css` | 1517 |
| Líneas eliminables inmediatamente | **~1100** (73%) |
| Líneas movibles a `Shared/*.razor.css` | **~250** (kanban, calendar, planner) |
| Líneas que se quedan (template Blazor + tokens) | **~150** |
| `<button>` crudos | 2 |
| `<input>` crudos | 0 |
| `<form>` crudos | 0 |
| `<a href>` (navegación interna) | 0 (los 6 son externos) |
| `<table>` crudos | 3 |
| `IJSRuntime.InvokeAsync` | 2 (1 XSS, 1 prompt nativo) |
| `RadzenDataGrid` actualmente en uso | 0 |
| `RadzenDataGrid` candidatos | 5+ (tablas + listas largas) |
| `RadzenTemplateForm` actualmente en uso | 34 (correcto) |
| Componentes shared nuevos a crear | 5-6 |
| Assets huérfanos (MB) | **~3 MB** de Bootstrap |
| Clases CSS huérfanas (referenciadas pero no definidas) | 50+ |
| Esfuerzo total estimado | **3-4 sesiones** |

---

**Siguiente paso**: leer [`02-plan.md`](02-plan.md) para el
plan de ejecución priorizado (3 oleadas, criterios de
aceptación, checklist por PR).
