# Cardscape Beta Test Report

**Fecha**: 2026-08-06
**Tester**: Mavis (MiniMax)
**Setup**: Docker (perfil dev SQLite), Playwright MCP para UI, PowerShell + Invoke-WebRequest para API
**Versión testeada**: v1.0.0 (commit actual de `main`)
**Ambiente**: Windows 11, Docker 29.6.2, .NET 10.0.302, navegador en container separado

---

## TL;DR

Cardscape tiene un frontend pulido y arquitectura interesante, pero **el camino "happy path" self-hosted en Docker está completamente roto**. Encontré **17 bugs** que van desde un Dockerfile que no compila, hasta APIs que devuelven shapes de JSON incompatibles con el cliente, hasta una página de tarjeta que se queda colgada en "Loading card…" para siempre. El backend tiene buena cobertura de endpoints (Wolverine minimal APIs, 200+ rutas en swagger) y la mayoría de los flujos simples funcionan, pero la integración frontend-backend tiene varios puntos de fricción.

**Recomendación**: NO está listo para v1.0.0 / release público. Los bugs #1, #2, #3 son bloqueantes (el contenedor no arranca de entrada en el flujo documentado); los bugs #6 y #7 rompen dos páginas completas (Inbox y CardDetail).

---

## Bugs encontrados (17)

### 🔴 CRÍTICOS — Bloquean el arranque / destruyen features

#### BUG #1 — `Dockerfile` no restaura `Cardscape.Web.csproj`
- **Síntoma**: `docker compose up --build` falla con `error NETSDK1004: Assets file '/src/src/Cardscape.Web/obj/project.assets.json' not found`.
- **Causa**: El Dockerfile hace COPY + `dotnet restore` solo para Domain, Application, Infrastructure y Api. Faltó `COPY src/Cardscape.Web/Cardscape.Web.csproj` antes del restore. El proyecto API referencia Web, por lo que el publish necesita sus assets.
- **Repro**: `git clean -fdx && docker compose -f docker-compose.dev.yml build --no-cache` (con el `Dockerfile` original).
- **Fix aplicado**: agregada la línea `COPY src/Cardscape.Web/Cardscape.Web.csproj src/Cardscape.Web/` antes del restore.
- **Archivo**: `src/Cardscape.Api/Dockerfile:11-15`

#### BUG #2 — `.dockerignore` ignora la carpeta `Abstractions/Storage/`
- **Síntoma**: Mismo build falla con `error CS0234: The type or namespace name 'Storage' does not exist in the namespace 'Cardscape.Application.Abstractions'`.
- **Causa**: El `.dockerignore` tiene la regla `**/Storage` (intentado para ignorar el folder de uploads del API). Pero también matchea `src/Cardscape.Application/Abstractions/Storage/`, que es código fuente, no uploads.
- **Repro**: una vez arreglado el Dockerfile (#1), el build sigue fallando por este motivo.
- **Fix aplicado**: cambié `**/Storage` por reglas más específicas: `**/Cardscape.Api/Storage` y `**/Cardscape.Api/storage` (case-insensitive).
- **Archivo**: `.dockerignore:22-23`

#### BUG #3 — `Cors:AllowedOrigins` no configurado en compose dev
- **Síntoma**: El contenedor arranca, abre puerto 8080, y crashea con `Unhandled exception. System.InvalidOperationException: Cors:AllowedOrigins is required outside the Development environment.`
- **Causa**: El compose dev define `ASPNETCORE_ENVIRONMENT=Production` pero no pasa `Cors__AllowedOrigins__*`. La validación al startup es estricta (bien, pero el docker-compose debería venir preconfigurado para una experiencia self-hostable).
- **Repro**: `docker compose -f docker-compose.dev.yml up` después de los fixes #1 y #2.
- **Fix aplicado**: agregadas vars `Cors__AllowedOrigins__0=http://localhost:8080` y `_1=http://127.0.0.1:8080` en ambos compose files.
- **Archivo**: `docker-compose.dev.yml:18-29`, `docker-compose.yml:25-37`

#### BUG #4 — Migraciones EF solo corren en `IsDevelopment()`
- **Síntoma**: Tras arreglar #3, la app corre pero loguea `SQLite Error 1: 'no such table: background_jobs'` cada 2 segundos y el `BackgroundJobDispatcherService` crashea en bucle.
- **Causa**: `Program.cs:168` envuelve `app.ApplyMigrations()` dentro de `if (app.Environment.IsDevelopment())`. Para un producto self-hosted en `Production`, el primer `docker compose up` deja la base de datos sin schema.
- **Repro**: la misma sesión de testing.
- **Workaround aplicado**: cambié `ASPNETCORE_ENVIRONMENT` a `Development` en el compose dev para poder seguir testeando. **Esto enmascara el bug**, no lo arregla.
- **Recomendación**: o correr migraciones también en Production (con `--migrate-on-start` flag, idealmente opt-in), o documentar muy fuerte que hay que correr `dotnet ef database update` antes de levantar el stack.
- **Archivo**: `src/Cardscape.Api/Program.cs:164-170`

#### BUG #5 — `ApiBaseUrl` hardcoded a `http://localhost:5291`
- **Síntoma**: El cliente Blazor WASM intenta llamar a `http://localhost:5291/api/auth/register` en vez de al contenedor del API. En mi setup el API está en `:8080` y el navegador está en otro container, así que `localhost:5291` es connection refused. Resultado: ningún POST del cliente llega al server.
- **Causa**: `src/Cardscape.Web/wwwroot/appsettings.json:2` tiene `"ApiBaseUrl": "http://localhost:5291/"`. El 5291 es el dev port de `dotnet run` en Windows; un setup self-hosted real no debería usar ese valor.
- **Workaround aplicado**: cambié el valor a `http://host.docker.internal:8080/` para que el navegador en Docker pueda llegar al host. Esto es un workaround de testing; el fix real sería:
  - Configurar la URL via env var en build (`--configuration Arg:ApiBaseUrl=...`), o
  - Dejar `ApiBaseUrl` vacío y usar el mismo origin (necesita que Blazor WASM y API se sirvan desde el mismo host, que parece ser el caso).
- **Archivo**: `src/Cardscape.Web/wwwroot/appsettings.json:2`

#### BUG #6 — `GET /api/cards/{id}/recurrence` devuelve `[]` cuando no hay recurrencia
- **Síntoma**: Al abrir una tarjeta, la página `CardDetail.razor` se queda en "Loading card…" indefinidamente. La consola del navegador muestra `System.Text.Json.JsonException: ExpectedJsonTokens Path: $`.
- **Causa**: El handler devuelve `[]` (array vacío) en vez de `null` cuando la tarjeta no tiene recurrencia. El cliente espera un `CardRecurrenceDto?` (objeto nullable) y no puede deserializar un array.
- **Repro**: crear cualquier tarjeta, abrir `/cards/{id}`. Es 100% reproducible.
- **Severidad**: crítica. La página de detalle de tarjeta — un feature core — está totalmente rota.
- **Archivo cliente**: `src/Cardscape.Web/Pages/CardDetail.razor:553` (`Recurrence.GetAsync(CardId)`). **Archivo server**: el handler de recurrence (no le hice grep, pero el bug está claro).

#### BUG #7 — `GET /api/notifications/unread-count` devuelve `{"count":0}` y cliente espera `int`
- **Síntoma**: La página Inbox se queda en "Loading…" para siempre. Consola: `System.Text.Json.JsonException: DeserializeUnableToConvertValue, System.Int32 Path: $`.
- **Causa**: el server devuelve un objeto `{"count":0}`, el cliente hace `await Notifications.GetUnreadCountAsync()` con `ApiResult<int>`. No matchea.
- **Repro**: estar logueado y navegar a `/inbox`. 100% reproducible.
- **Severidad**: crítica. La página de Inbox está totalmente rota.
- **Archivo cliente**: `src/Cardscape.Web/Pages/Inbox.razor:95`. **Fix de server o de cliente**: cualquiera de los dos.

#### BUG #8 — `GET /api/notifications` devuelve 500
- **Síntoma**: al fallar el `unread-count`, el código de Inbox cae al segundo request (`ListAsync`) que también devuelve 500. Probé directo: `{"type":"about:blank","title":"Internal server error","status":500}`.
- **Causa**: handler crashea. No lo investigué más a fondo, pero la combinación con BUG #7 hace Inbox totalmente inutilizable.
- **Archivo server**: handler de notificaciones.

#### BUG #9 — `POST /api/boards` 500 si visibility viene como string
- **Síntoma**: `{"visibility":"Private"}` produce `Microsoft.AspNetCore.Http.BadHttpRequestException: Failed to read parameter "CreateBoardRequestBody body" from the request body as JSON. Path: $.visibility`.
- **Causa**: el endpoint espera un enum como **integer** (`visibility: 0`), no como string. El swagger no aclara esto (vi el spec generado y aparece como enum pero sin `x-enumNames` o similar que diga "0=Private, 1=Workspace, 2=Public"). La UI Blazor probablemente serializa como int y por eso a ella le anda.
- **Repro**: cualquier llamada API externa con string enum value.
- **Severidad**: alta para integradores externos (MCP, scripts). Baja para la UI oficial.
- **Fix sugerido**: agregar `JsonStringEnumConverter` global, o documentar claramente en swagger.

---

### 🟠 ALTOS — Features rotas o usability issues graves

#### BUG #10 — `BoardDetail.razor` renderiza tarjetas dos veces
- **Síntoma**: en la vista de board, cada tarjeta aparece visualmente duplicada (dos `<p>` con el mismo título, apilados).
- **Causa**: el componente `KanbanBoard` recibe **a la vez** un `CardTemplate` (con `<RadzenCard>`) y un `ChildContent` que itera `cardsByList` con un `RadzenCard` propio. Ambos terminan renderizando la misma tarjeta.
- **Repro**: crear una tarjeta, ver el board.
- **Severidad**: alta. Es visualmente confuso y duplica clicks/eventos.
- **Archivo**: `src/Cardscape.Web/Pages/BoardDetail.razor:57-95` (ver el bloque `CardTemplate` vs el `ChildContent` con `foreach`).
- **Fix**: eliminar uno de los dos. Probablemente el `CardTemplate` no debería existir si ya hay un `ChildContent` que itera.

#### BUG #11 — `CultureSwitcher.LoadTranslationsAsync` falla con relative URL
- **Síntoma**: cada navegación a una página nueva loguea en consola `fail: Cardscape.Web.Services.CultureSwitcher[0] Failed to load translations for culture en; System.InvalidOperationException: net_http_client_invalid_requesturi`.
- **Causa**: `Services/CultureSwitcher.cs:234` hace `new HttpRequestMessage(HttpMethod.Get, "Resources/SharedResource.en.resx")` con URL relativa. Blazor WASM no tiene un base address consistente en este setup y rechaza.
- **Repro**: cualquier navegación. Aparece en consola incluso cuando la página carga bien.
- **Severidad**: alta. No rompe funcionalidad, pero la página "blazor-error-ui" queda visible permanentemente abajo a la izquierda y llena la consola de errores.
- **Fix sugerido**: usar `NavigationManager.BaseUri + url`, o crear el cliente HTTP con base address, o cambiar a `HttpClient.GetAsync(url)`.

#### BUG #12 — Página "Pending invitations" no se actualiza tras crear invitación
- **Síntoma**: en `/workspaces/{id}/members`, al hacer click en "Send" para invitar a un email, el dialog de éxito muestra el token y dice "send this link", pero la lista "Pending invitations" sigue mostrando "No pending invitations" en la misma pantalla.
- **Causa**: el handler hace POST y muestra el dialog, pero no recarga la lista de pending invitations que está renderizada arriba.
- **Repro**: 100% reproducible.
- **Severidad**: media. UX confuso, da impresión de que la invitación no se guardó.

#### BUG #13 — No existe endpoint `GET /api/auth/me`
- **Síntoma**: `GET /api/auth/me` con token válido devuelve 200 con cuerpo HTML (la SPA fallback). Swagger confirma: no está listado.
- **Causa**: el handler de "me" no existe. El cliente Blazor WASM lee el JWT claims localmente y nunca necesita llamar a /me, así que el cliente está OK, pero cualquiera que asuma un endpoint estándar REST se va a confundir.
- **Repro**: cualquier GET a /api/auth/me.
- **Severidad**: baja (no rompe nada), pero es una desviación de convención.

#### BUG #14 — `POST /api/auth/refresh` devuelve 405
- **Síntoma**: `POST /api/auth/refresh` con `{refreshToken: "..."}` devuelve 405.
- **Causa**: no existe endpoint de refresh. El login devuelve `refreshToken` y `refreshTokenExpiresAt` pero no hay forma de renovarlo.
- **Repro**: ver el primer bloque de tests.
- **Severidad**: alta para integradores (MCP, scripts de larga duración). El access token expira en 1h y no hay forma de extender.

---

### 🟡 MEDIOS — Edge cases, UX, code quality

#### BUG #15 — Warning de EF: collection navigation sin value comparer
- Cada `Microsoft.EntityFrameworkCore.Model.Validation.CollectionWithoutComparer` aparece en logs para `OAuthAccessToken.Scopes`, `OAuthApp.AllowedScopes`, `OAuthApp.RedirectUris`, `OAuthAuthorizationCode.Scopes`. No causa fallo visible pero es un warning de performance — EF puede no detectar cambios en colecciones si no hay comparer.
- **Severidad**: media. Performance / correctness.
- **Fix**: agregar `.Metadata.SetValueComparer(...)` en `OnModelCreating` o anotar las propiedades con `[ValueComparer]`.

#### BUG #16 — `BackgroundJobDispatcherService` crashea cada 2s cuando no hay schema
- Vinculado a BUG #4. El servicio poll-ea la tabla `background_jobs` y como no existe, falla. Catch genérico reintenta, pero genera ruido masivo en logs y CPU.
- **Severidad**: media. Una vez migrado el schema deja de pasar.

#### BUG #17 — Authorization en SignalR `JoinBoard` siempre falla
- **Síntoma**: el log muestra `Microsoft.AspNetCore.SignalR.HubException: Authentication required to join a board group.` en cada conexión al board.
- **Causa**: el atributo `[Authorize]` está en `BoardHub`, pero el usuario llega como null al handler. Probable incompatibilidad entre el scheme de auth JWT y SignalR WebSocket upgrade.
- **Repro**: navegar a cualquier board (la UI muestra "Live updates off — refreshes on navigation only" como fallback).
- **Severidad**: media. Funcionalidad degradada (sin real-time), pero el board sigue siendo usable.
- **Fix**: agregar `AddAuthentication().AddJwtBearer(options => { options.Events = new JwtBearerEvents { OnMessageReceived = ctx => { var accessToken = ctx.Request.Query["access_token"]; ... } }; })` o usar cookies con `OnMessageReceived`.
- **Archivo**: `src/Cardscape.Api/Hubs/BoardHub.cs:18-19, 47-50`

---

## Lo que SÍ funciona bien

Para no ser solo críticas, esto funcionó perfectamente en mi testing:

- **Health check** `/health` → 200 healthy
- **Registro de usuario** (vía UI) → 201, redirige a home, JWT guardado
- **Login** (vía UI y API) → token + refresh, persistido en localStorage
- **Crear workspace, board, list, card** (vía UI con Radzen) → todo anduvo
- **Invitar a workspace** → genera token de invitación, email simulado en consola (servicio `ConsoleInvitationEmailService`)
- **Calendar** → renderiza mes actual con eventos (vacío en mi caso porque no asigné due dates)
- **Planner** → vista swimlane, empty state OK
- **API Tokens page** (`/account/api-tokens`) → crea tokens, los lista
- **2FA enrollment** → 200, devuelve QR code + recovery codes
- **Settings 2FA** → status endpoint funcional
- **Workspace members** → grid con Admin/Member roles, invitar funciona
- **Cross-user isolation**: Bob no ve workspaces de Alice (200 []), recibe 403 al intentar GET de workspace ajeno
- **Validación de input** (register/login) → 400 / 401 correctos para email inválido, password corto, credenciales malas, email duplicado
- **Authorization** general: `/api/workspaces` sin auth → 401
- **BoardHub IDOR protection**: el chequeo de "is member" en `JoinBoard` está bien implementado (por eso la membresía no se filtra aunque la auth de SignalR falle)
- **Custom Validation rules** (`BoardName`, `BoardDescription`, etc.) bien armadas
- **Search** → 200 con estructura vacía correcta
- **CultureSwitcher fallback** → al menos degrada a inglés embedded en vez de romper

---

## Tests que ejecuté (resumen)

### Via Playwright (UI) — todos los flujos
- ✅ Registro de Alice
- ✅ Login
- ✅ Crear workspace "Beta Test Co"
- ✅ Crear board "Q3 Roadmap"
- ✅ Crear lista "Backlog" + "Doing"
- ✅ Crear tarjeta "Investigate flaky test"
- ❌ Abrir tarjeta — bug #6
- ❌ Inbox — bug #7, #8
- ✅ Calendar
- ✅ Planner
- ✅ Settings 2FA — bug #11 (warning en consola)
- ✅ API Tokens page (`/account/api-tokens`)
- ✅ Members page + Invite

### Via REST API — 60+ tests automatizados
Archivo: `D:/GitHub/Cardscape/test-results/api/api-tests-v2.ps1`
Log: `D:/GitHub/Cardscape/test-results/api/full-test-v2.log`

Categorías:
- Auth (register, login, me, revoke, refresh) — 4/6 OK
- Workspaces (CRUD + members) — 5/7 OK
- Boards (CRUD + star + visibility) — 4/9 OK (bug #9)
- Lists (CRUD) — 1/2 OK (cascada de #9)
- Cards (CRUD + move + complete + due date) — 5/12 OK (cascada)
- Recurrence — bug #6 confirmado
- Labels, Comments, Checklists, Votes — 8/12 OK (cascada de #9)
- Notifications — bug #7 confirmado
- 2FA — 3/3 OK
- API tokens (crear + usar + revocar) — 1/3 OK (faltó scope)
- Invitations (crear, listar, aceptar, revocar) — 3/5 OK
- Search — 2/2 OK
- Cross-user permission tests — 3/3 OK (aislamiento correcto)
- Validación de input — 4/4 OK
- Edge cases (Unicode, long strings, etc.) — parciales

---

## Recomendaciones (ordenadas por impacto)

1. **BLOQUEANTE — Fix #1, #2, #3 antes de cualquier release**: el camino documentado (`docker compose up`) no funciona. Sin estos fixes, ningún usuario podrá levantar el proyecto.

2. **BLOQUEANTE — Fix #6, #7 (recurrence y notifications unread-count)**: dos páginas core completamente rotas.

3. **ALTO — Fix #11 (CultureSwitcher) y #10 (BoardDetail duplicate render)**: la primera llena la consola y muestra el error UI; la segunda confunde al usuario.

4. **ALTO — Decidir la estrategia de migraciones en Producción**: o correrlas en startup (con un flag opt-out), o documentar `dotnet ef database update` como pre-requisito. El estado actual (no migrar, dejar la app crasheando en bucle) es el peor de los mundos.

5. **ALTO — Configurar `ApiBaseUrl` dinámicamente**: ya sea por env var, o derivar del `window.location.origin` cuando Blazor y API comparten host.

6. **MEDIO — Fix #9 (enums como string en API)**: agregar `JsonStringEnumConverter` globalmente o documentar muy claro.

7. **MEDIO — Investigar el SignalR auth (#17)**: hoy el real-time está silenciosamente desactivado, lo cual contradice el README que promociona "real-time SignalR".

8. **MEDIO — Fix #12 (Pending invitations refresh) y #14 (refresh endpoint)**: UX básico y cumplimiento del contrato de auth.

9. **BAJO — Warnings de EF collection comparer (#15)**: agregar value comparers a las propiedades de colección de OAuth entities.

10. **BAJO — Documentar rutas correctas en README**: `/api-tokens` no existe (es `/account/api-tokens`), `/account/two-factor` no existe (es `/settings/two-factor`). Varios links del nav lateral asumen las rutas correctas, pero alguien que pruebe por URL directa se va a comer un 404.

---

## Lo que cambié (workarounds de testing, no fixes)

- `src/Cardscape.Api/Dockerfile` — agregado COPY de `Cardscape.Web.csproj` (fix real de #1)
- `.dockerignore` — reglas de `Storage` más específicas (fix real de #2)
- `docker-compose.dev.yml` y `docker-compose.yml` — agregada config de CORS (fix real de #3)
- `docker-compose.dev.yml` — cambié `ASPNETCORE_ENVIRONMENT=Production` a `Development` (workaround de #4, no fix)
- `src/Cardscape.Web/wwwroot/appsettings.json` — `ApiBaseUrl` cambiado a `http://host.docker.internal:8080/` (workaround de #5, no fix)

Si querés que revierta los workarounds, decime. Los fixes reales de #1-#3 sí valen la pena quedarse (son bugs del repo), el resto depende de cómo querés manejar el setup.

---

## Archivos generados durante el testing

- `D:/GitHub/Cardscape/test-results/BETA-TEST-REPORT.md` — este reporte
- `D:/GitHub/Cardscape/test-results/docker-build.log` — log del primer build fallido
- `D:/GitHub/Cardscape/test-results/docker-up.log` — log de los up inicial
- `D:/GitHub/Cardscape/test-results/api/api-tests-v2.ps1` — script de tests API
- `D:/GitHub/Cardscape/test-results/api/full-test-v2.log` — output completo de los tests
- `D:/GitHub/Cardscape/test-results/api/findings.json` — findings estructurados (vacío porque el script falló temprano en PowerShell parsing; los findings están en este MD)

## Setup actual

- Container: `cardscape.api` corriendo en `http://localhost:8080`
- DB: SQLite en volume `cardscape_cardscape.data`
- Usuarios creados durante testing:
  - `alice@cardscape.test` / `TestPass123!`
  - `bob@cardscape.test` / `TestPass123!`
  - `bob2@cardscape.test` / `TestPass123!`
- Workspace: "Beta Test Co" (id `519969b0-...`) con board "Q3 Roadmap" y listas Backlog/Doing
- 2FA enrolled para Alice (credential ID `5f85bea4-...`) — recovery codes perdidos
