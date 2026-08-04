# Refactoring de la UI de Cardscape hacia Radzen puro

> **Status**: ✅ **Completed** (2026-08-04). The whole UI is now
> Radzen.Blazor; `app.css` is down to < 100 lines; the Bootstrap
> assets are gone; the `IJSRuntime.InvokeAsync` `eval` XSS vector
> is gone. See [ADR 0009 — Radzen-only UI](../adr/0009-radzen-only-ui.md)
> for the decision record and
> [`docs/roadmap/05-plan-v1.2.0.md`](../roadmap/05-plan-v1.2.0.md)
> for the next chunk of work.

> **Objetivo (histórico)**: eliminar TODO el HTML, JS y CSS custom
> del cliente Blazor (`src/Cardscape.Web/`) y reemplazarlo por
> componentes **Radzen.Blazor** nativos. La única excepción
> permitida era **CSS isolation de Blazor** (archivos `.razor.css`
> scoped) para vistas que Radzen no cubre (kanban, calendar
> mensual, planner mensual) y que no justifican re-implementarse
> con `RadzenCard`s.
>
> **Cero modificaciones** en este documento — es histórico. La
> auditoría inicial ([`01-audit.md`](01-audit.md)) y el plan
> detallado ([`02-plan.md`](02-plan.md)) se conservan como
> referencia de qué se hizo y por qué.

## Índice

| # | Documento | Qué contiene |
|---|---|---|
| 1 | [`01-audit.md`](01-audit.md) | **Auditoría completa** del estado inicial (HTML/JS/CSS custom por página, clases huérfanas, assets muertos). El "qué había antes". |
| 2 | [`02-plan.md`](02-plan.md) | **Plan de ejecución** priorizado en 3 oleadas (P0 / P1 / P2), con componentes shared nuevos, criterios de aceptación y checklist por PR. El "qué se hizo, en qué orden y cómo". |
| 3 | [ADR 0009](../adr/0009-radzen-only-ui.md) | Decisión arquitectónica: Cardscape.Web usa Radzen.Blazor exclusivamente, con tres componentes shared en CSS isolation. |
| 4 | [`05-plan-v1.2.0.md`](../roadmap/05-plan-v1.2.0.md) | El próximo chunk (doc reconciliation, i18n follow-up, integration-test stability, CI coverage diff). |

## Estado final (post-refactor)

| Métrica | Inicial (2026-08-03) | Final (2026-08-04) |
|---|---:|---:|
| Líneas `app.css` | 1517 | **< 100** |
| `<button>` en `Pages/` | 2 | **0** |
| `<input>` en `Pages/` | 0 | 0 |
| `<form>` en `Pages/` | 0 | 0 |
| `IJSRuntime.InvokeAsync` en `Pages/` | 2 (1 con XSS real) | **0** |
| `RadzenDataGrid` en uso | 0 | 8+ |
| Componentes shared `.razor` | 1 | **8** (PageHeader, LabeledField, MetadataList, KanbanBoard, HorizontalRule, SecretBox, ConfirmCodeDialog, InboxBell) |
| Componentes shared con CSS isolation (`.razor.css`) | 0 | **3** (KanbanBoard, HorizontalRule, SecretBox) |
| Assets Bootstrap | ~3 MB | **0** |
| Clases CSS huérfanas | 50+ | **0** |

## Qué se construyó (resumen)

- **6 componentes shared nuevos** en `src/Cardscape.Web/Shared/`
  ([`PageHeader`](src/Cardscape.Web/Shared/PageHeader.razor),
  [`LabeledField`](src/Cardscape.Web/Shared/LabeledField.razor),
  [`MetadataList`](src/Cardscape.Web/Shared/MetadataList.razor),
  [`KanbanBoard`](src/Cardscape.Web/Shared/KanbanBoard.razor),
  [`ConfirmCodeDialog`](src/Cardscape.Web/Shared/ConfirmCodeDialog.razor),
  [`SecretBox`](src/Cardscape.Web/Shared/SecretBox.razor),
  [`HorizontalRule`](src/Cardscape.Web/Shared/HorizontalRule.razor),
  [`InboxBell`](src/Cardscape.Web/Shared/InboxBell.razor)).
- **3 archivos `.razor.css`** para los componentes que Radzen
  no cubre end-to-end (kanban, divisores, secretos de API tokens).
- **0 dependencias externas** (sin CDN, sin Google Fonts, sin
  Bootstrap). Barlow se sirve desde `wwwroot/fonts/`.
- **0 `IJSRuntime.InvokeAsync`** en `Pages/` (el vector XSS
  del `eval` en `OAuthCallback.razor` se reemplazó por
  `NavigationManager.Uri`).
- **Build verde** (0 errores, 0 warnings en 11/11 proyectos).
- **Tests verdes** (343 unit + 10 architecture + 1 functional
  + 100 integration).

## Por qué este refactor

(Histórico — preservado como referencia.)

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
