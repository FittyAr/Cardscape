# Refactoring de la UI de Cardscape hacia Radzen puro

> **Objetivo**: eliminar TODO el HTML, JS y CSS custom del cliente
> Blazor (`src/Cardscape.Web/`) y reemplazarlo por componentes
> **Radzen.Blazor** nativos. La única excepción permitida es
> **CSS isolation de Blazor** (archivos `.razor.css` scoped) para
> vistas que Radzen no cubre (kanban, calendar mensual, planner
> mensual) y que no justifican re-implementarse con `RadzenCard`s.
>
> **No hay "después".** Cada elemento custom listado en este
> documento tiene un destino Radzen concreto y un orden de
> ejecución. La regla de oro es:
>
> > *"Si Radzen lo provee, lo usamos. Si Radzen no lo provee, lo
> > movemos a un componente shared con CSS isolation. Nunca
> > dejamos CSS huérfano en `app.css`."*

## Índice

| # | Documento | Qué contiene |
|---|---|---|
| 1 | [`01-audit.md`](01-audit.md) | **Auditoría completa** del estado actual: HTML/JS/CSS custom por página, clases huérfanas, assets muertos. El "qué hay hoy". |
| 2 | [`02-plan.md`](02-plan.md) | **Plan de ejecución** priorizado en 3 oleadas (P0 / P1 / P2), con componentes shared nuevos, criterios de aceptación y checklist por PR. El "qué hacer, en qué orden y cómo". |

## Resumen ejecutivo (TL;DR)

- **CSS custom eliminable de inmediato**: **~1100 de 1517 líneas**
  (~73 %) de `wwwroot/css/app.css`.
- **HTML crudo que queda en `.razor`**: **0** `<form>`, **0**
  `<input>`, **2** `<button>` (Calendar, Planner), **3** `<table>`.
- **`IJSRuntime` que puede eliminarse**: **2** sitios, uno de
  ellos con un vector XSS real (`OAuthCallback.razor:48`).
- **Assets huérfanos**: **~3 MB de Bootstrap** en
  `wwwroot/lib/bootstrap/dist/` (no referenciados en ningún
  lugar de la app Blazor).
- **Componentes shared nuevos a crear**: 5
  (`PageHeader`, `LabeledField`, `MetadataList`, `KanbanBoard`,
  `MonthCalendar`, `MonthPlanner`).
- **Tablas candidatas a `RadzenDataGrid`**: 3 explícitas + 5
  listas largas.
- **Esfuerzo total estimado**: **3-4 sesiones** de trabajo
  enfocado para llegar a "100 % Radzen + CSS isolation de
  Blazor" en los componentes custom que lo requieran.

## Por qué este refactor

1. **Reducir superficie de mantenimiento**. Un `app.css` con
   clases para "auth-shell", "auth-card", "auth-field",
   "inbox-item--unread" y "planner-card" en el mismo archivo es
   deuda que va a seguir creciendo.
2. **Aprovechar lo que Radzen ya da gratis**: validación,
   accesibilidad WCAG, responsive, theming (cookie dark/light),
   `DataGrid` con sort/filter/paginate, `TemplateForm` con
   binding, `Dialog`, `Notification`, `Tooltip`,
   `ContextMenu`, `ProfileMenu`.
3. **Eliminar anti-patrones de seguridad**: el `JS.InvokeAsync`
   con `eval` en `OAuthCallback.razor` ejecuta JS arbitrario y
   tiene que irse.
4. **Alinear con el contrato del proyecto**. El
   `docs/AGENTS.md` sección 8 ya declara `radzen-blazor` como
   skill obligatorio para tocar la UI. Vamos a hacer que la
   realidad del código refleje la regla.

## Reglas de aceptación (válidas para cada PR de este refactor)

1. **Cero clases custom en `app.css`** que no estén dentro de
   `:root`, `#blazor-error-ui`, `.blazor-error-boundary` o
   `.loading-progress*` (los 4 elementos que el template Blazor
   WASM requiere para el spinner de carga y el overlay de error).
2. **Cero `<button>`, `<input>`, `<form>`, `<a href="...">`
   (navegación interna) en `.razor`** que no sean
   `RadzenButton`/`RadzenTextBox`/`RadzenTemplateForm`/
   `RadzenLink`/`NavLink`.
3. **Cero `IJSRuntime.InvokeAsync`**. Excepción justificada: que
   el componente Radzen correspondiente no exista y se documente
   en `docs/adr/`.
4. **CSS scoped en `.razor.css`** para los 3 componentes shared
   que Radzen no cubre (`KanbanBoard`, `MonthCalendar`,
   `MonthPlanner`). Se documenta el motivo en cada archivo.
5. **Build verde, tests verdes, accesibilidad WCAG AA
   preservada** (incluido el `prefers-reduced-motion`).
