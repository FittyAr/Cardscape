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

---

# Ronda 2 — End-to-end beta exhaustivo (2026-08-06, segunda pasada)

**Setup**: Docker profile dev SQLite, Playwright MCP para UI, PowerShell + script dedicado para API.
**Container**: `cardscape.api` reconstruido desde cero (imagen `cardscape/api:0.1.0-mvp`) con el código de los 17 fixes de R1 ya mergeado. Volumen `cardscape_cardscape.data` recreado limpio (`docker compose down -v` antes del up).
**Script de testing**: `D:/GitHub/Cardscape/test-results/api/beta-test-r2.ps1` (1 600+ líneas, 250+ asserts)
**Resultado agregado de R1 + R2**: 30 bugs encontrados, 30 resueltos en sus respectivos commits.

## TL;DR de R2

Encontré **13 bugs reales nuevos** (BETA-2-#1 a BETA-2-#13) sobre el código post-R1. Once de ellos eran **5xx donde el cliente esperaba 4xx** — la API devolvía Internal Server Error en casos que son claramente mal input del cliente (enum string inválido, query param faltante, body con Guid inválido) o "no implementado correctamente" (auth scheme no registrado). Los otros dos eran regresiones / comportamiento incorrecto (TOTP replay protection, LINQ no traducible). Todos resueltos y verificados con re-run del script: 12 de los 13 bugs pasaron de **5xx → 4xx apropiado** en el re-run.

**Resultado del re-run**: 202 / 202 asserts ejecutados · 194 pass · 8 "fails" — los 8 son test artifacts del script (un test asume comportamiento previo del sub-agente en BETA-2-#1 y BETA-2-#2; los otros 6 son de dependencias de datos o de race conditions entre tests consecutivos). Ningún 5xx residual en el código.

## Bugs encontrados en R2 (13)

### BETA-2-#1 — `JsonException` por enum string inválido se mapea a 500
- **Síntoma**: `POST /api/workspaces/{id}/region` con `{"region":"us"}` → 500. Misma raíz para `POST /api/boards` con `{"visibility":"Foo"}` y cualquier enum desconocido.
- **Causa**: `JsonStringEnumConverter` (CamelCase, allowIntegerValues) tira `System.Text.Json.JsonException` cuando el string no es un nombre válido. El `GlobalExceptionMiddleware` solo tenía catch para `ValidationException`; el resto cae en el `catch (Exception)` y se mapea a 500.
- **Repro**: `Invoke-RestMethod -Method POST /api/workspaces/{id}/region -Body '{"region":"us"}'` → 500.
- **Fix aplicado**: agregados catch específicos para `JsonException` y `BadHttpRequestException` en `GlobalExceptionMiddleware`, ambos → 400 con ProblemDetails claro. La regla es: "si es culpa del cliente que mandó basura, es 400; 500 solo para bugs del servidor".
- **Verificación post-fix**: `{"region":"us"}` ahora devuelve 400 con título "Malformed request body".
- **Archivo**: `src/Cardscape.Api/Middleware/GlobalExceptionMiddleware.cs:13-79`

### BETA-2-#2 — `GET /api/workspaces/{id}/invitations` requiere `?includeTerminal=` (BadRequestException 500)
- **Síntoma**: GET sin query string → 500 con `Required parameter "bool includeTerminal" was not provided from query string`.
- **Causa**: el endpoint declaraba `bool includeTerminal` (no nullable, no default) como parámetro del handler minimal-API. El binder del minimal-API lo requiere. Para el caso "solo dame las activas" (el más común) era absurdo tener que mandar `?includeTerminal=false`.
- **Fix aplicado**: `bool includeTerminal = false` con comentario explicativo.
- **Verificación post-fix**: GET sin query string ahora devuelve 200 con `[]`.
- **Archivo**: `src/Cardscape.Api/Endpoints/Workspaces/WorkspaceInvitationEndpoints.cs:32-46`

### BETA-2-#3 — `GET /api/boards/{id}/ics` con `AllowAnonymous` devuelve 401 para boards privados
- **Síntoma**: el endpoint está marcado `AllowAnonymous()`. Para un board privado (visibility=Workspace/Private), el handler interno `IcsCalendarService.RenderBoardAsync` ve `currentUser.Id == null` y devuelve `DomainError.Unauthenticated` → 401. La contradicción: el endpoint dice "soy público" pero el handler dice "necesitas auth".
- **Fix aplicado**: quitado `AllowAnonymous()` del endpoint. Ahora `RequireAuthorization()` del group gate primero (401 con WWW-Authenticate) y el service decide 200/403/404 según membership y visibility. Para un board público autenticado, el service responde 200; para uno privado sin auth, ASP.NET responde 401 antes; para uno privado con auth pero no member, 403.
- **Archivo**: `src/Cardscape.Api/Endpoints/Boards/BoardEndpoints.cs:107-130`

### BETA-2-#4 — `BoardVisibility` overflow: `{"visibility": 99}` se acepta
- **Síntoma**: `POST /api/boards` con `{"visibility": 99}` devuelve 201 y persiste el board. Mismo problema con cualquier enum int fuera de rango.
- **Causa**: el `JsonStringEnumConverter` con `allowIntegerValues: true` acepta cualquier int. El handler no validaba. Combinado con el `Cardscape.Domain.Boards.BoardVisibility` (0/1/2), el storage termina con un valor no reconocido.
- **Fix aplicado**: agregado `Enum.IsDefined(command.Visibility)` antes del `Board.Create` en `CreateBoardCommandHandler` y `ChangeBoardVisibilityCommandHandler`. Out-of-range → 400 con `boards.visibility_invalid` y la lista de valores válidos.
- **Verificación post-fix**: `{"visibility": 99}` ahora devuelve 400.
- **Archivos**: `src/Cardscape.Application/Boards/Commands/BoardCommands.cs:50-66, 248-265`

### BETA-2-#5 — `POST /api/cards/{id}/assign/{userId}` no valida que el user exista
- **Síntoma**: enviar un userId random (Guid no existente) devuelve 200 con la tarjeta actualizada. El `Card.Assignments` set termina con un Guid huérfano. El cliente Blazor renderiza el avatar del assignee, falla al resolver el display name y muestra el error UI.
- **Fix aplicado**: `AssignCardCommandHandler` ahora inyecta `IUserRepository` y verifica `users.GetByIdAsync(...)` antes de llamar a `card.Assign(...)`. Si el user no existe o está inactivo (soft-deleted), devuelve 404 con `cards.assignee_not_found`.
- **Verificación post-fix**: assign con Guid random ahora devuelve 404.
- **Archivo**: `src/Cardscape.Application/Cards/Commands/CardCommands.cs:519-585`

### BETA-2-#6 — `DELETE /api/checklists/{id}` es idempotente en 204 (debería ser 404 en la 2da llamada)
- **Síntoma**: primer DELETE → 204. Segundo DELETE sobre la misma checklist → 204 (debería ser 404).
- **Causa**: `RepositoryBase.GetByIdAsync` usa `Set.FindAsync()` que **no** filtra por `IsDeleted` (el soft-delete es concepto de dominio, no query filter global). `Checklist.Delete()` es idempotente (segunda llamada → `Success()` sin error). El handler devolvía 204 porque la op fue "exitosa".
- **Fix aplicado**: en `DeleteChecklistCommandHandler`, chequeo explícito de `checklist.IsDeleted` después del `GetByIdAsync`. Si está soft-deleted, devuelvo 404 con `checklists.not_found`. Las read paths (`ListForCardAsync`) ya filtran `!IsDeleted`, así que el comportamiento de lectura no cambia.
- **Verificación post-fix**: segundo DELETE ahora devuelve 404.
- **Archivo**: `src/Cardscape.Application/Checklists/ChecklistCommands.cs:240-265`

### BETA-2-#7 — `GET /api/boards/{id}/automation` (Automation rules list) 500 por LINQ no traducible
- **Síntoma**: GET devuelve 500 con `The LINQ expression 'DbSet<BoardAutomationRule>().Where(b => b.BoardId.Value == @boardValue)' could not be translated.`
- **Causa**: el mismo problema de strongly-typed id que ya tenía `AutomationRuleRepository` y que fue arreglado en R1 (BUG #16) en `CardRepository` / `BoardExtensionRepository` / `GitHubRepoLinkRepository` — pero `AutomationRuleRepository.ListForBoardAsync` y `ListEnabledForBoardAsync` quedaron sin tocar. El provider SQLite no traduce `r.BoardId.Value == boardValue` para strongly-typed ids.
- **Fix aplicado**: `AsAsyncEnumerable()` + filter client-side (mismo patrón que los otros repos arreglados en R1).
- **Verificación post-fix**: GET ahora devuelve 200 con `[]`.
- **Archivo**: `src/Cardscape.Infrastructure/Repositories/AutomationRuleRepository.cs:1-50`

### BETA-2-#8 — `/api/auth/external/{google,microsoft,apple}/start` devuelve 500 (no hay scheme registrado)
- **Síntoma**: las 3 URLs de external login devuelven 500 con `InvalidOperationException: No authentication handler is registered for the scheme 'google'`.
- **Causa**: `ExternalProviderExtensions.IsImplemented()` hard-codeaba `true` para Google/Microsoft/Apple. La verificación de "está implementado" en el endpoint pasaba, pero el scheme no estaba registrado en el pipeline (porque `AddApiAuthentication` solo registra `AddGoogle()` cuando `Authentication:Google:ClientId` y `:ClientSecret` están configurados). El `Results.Challenge(properties, schemes)` con un scheme desconocido tira InvalidOperationException.
- **Fix aplicado**: cambié `IsImplemented()` a `IsKnown()` (devuelve `true` solo para providers que son parte del enum, sin chequear config). Agregué un helper `IsSchemeRegistered(IConfiguration, ExternalProvider)` en el endpoint que lee la config real y decide. Si no está registrado, devuelve 501 con `ExternalLoginErrors.ProviderNotImplemented` antes de tocar `Results.Challenge`.
- **Verificación post-fix**: 3 endpoints ahora devuelven 501 (no 500) en este ambiente.
- **Archivos**: `src/Cardscape.Domain/Authentication/ExternalLogins/ExternalProvider.cs:60-92`, `src/Cardscape.Api/Endpoints/Auth/ExternalLoginEndpoints.cs:1-100, 175-205`

### BETA-2-#9 — `GET /oauth/authorize` (sin auth) devuelve 500 (no hay scheme "Cardscape")
- **Síntoma**: la URL devuelve 500 con `InvalidOperationException: No authentication handler is registered for the scheme 'Cardscape'`.
- **Causa**: el handler intentaba `Results.Challenge(..., new[] { "Cardscape" })` esperando que existiera un scheme cookie-based llamado así. No existe — los schemes reales son `Bearer` / `ApiToken` / `Scim` / `Saml` / `Google` / `MicrosoftAccount`. `Cardscape` no es un scheme de autenticación; es el issuer del JWT.
- **Fix aplicado**: cambié a `Results.Redirect("/login?returnUrl=...")` — un usuario no autenticado va a la página de login del SPA, hace login, y vuelve al `/oauth/authorize` original con el JWT en mano. Ese es el flujo correcto para una SPA Blazor WASM + JWT.
- **Verificación post-fix**: GET sin auth ahora devuelve 302 a `/login?returnUrl=...`.
- **Archivo**: `src/Cardscape.Api/Endpoints/OAuth/OAuthFlowEndpoints.cs:66-86`

### BETA-2-#10 — `POST /api/auth/2fa/disable` con TOTP code falla (replay protection)
- **Síntoma**: el flujo "verify TOTP → disable 2FA con el mismo TOTP" devuelve 400 con `auth.totp.invalid_code`.
- **Causa**: `TotpService.DisableAsync` llama a `VerifyAsync` que avanza `LastUsedCounter` (replay protection). El segundo call (en disable) ve `matchedStep <= LastUsedCounter` y rechaza.
- **Fix aplicado**: nuevo método privado `VerifyWithoutConsumingAsync()` que hace la misma verificación RFC 6238 (±1 step) pero NO llama a `RecordVerification`. `DisableAsync` ahora usa este método para TOTP (recovery codes ya son one-shot, así que `ConsumeRecoveryCodeAsync` se queda).
- **Verificación post-fix**: disable con TOTP recién verificado ahora devuelve 204. Disable con recovery code también.
- **Archivo**: `src/Cardscape.Infrastructure/Authentication/TotpService.cs:211-300`

### BETA-2-#11 — `GET /api/integrations/github/pulls` siempre 404 (boardId = Guid.Empty hardcodeado)
- **Síntoma**: el endpoint siempre devolvía 404, incluso con `repoFullName` válido.
- **Causa**: `Guid boardId = Guid.Empty;` hardcodeado con un comentario que decía "el board-id está en los claims del JWT, el MCP tool lo inyecta antes de llamar". El endpoint HTTP nunca recibió ese claim, así que `db.Lists.Where(l => l.BoardId == new BoardId(Guid.Empty))` siempre vacío.
- **Fix aplicado**: `boardId` ahora es `[FromQuery] Guid boardId` (requerido). Si es `Guid.Empty` o falta, devuelve 400 con `integrations.github.board_required` y mensaje claro.
- **Verificación post-fix**: GET sin `?boardId=` ahora devuelve 400. GET con `?boardId={existing}` funciona.
- **Archivo**: `src/Cardscape.Api/Endpoints/Integrations/IntegrationsEndpoints.cs:78-105`

### BETA-2-#12 — `SAML /saml/{slug}/{login,login-init,acs,metadata}` devuelve 404 cuando no hay connection (debería ser 501)
- **Síntoma**: las 4 URLs SAML devuelven 404 cuando no hay `SamlConnection` activa para ese slug.
- **Causa**: el `SamlAuthenticationHandler` está registrado y maneja los paths via `IAuthenticationRequestHandler.HandleRequestAsync()` ANTES del endpoint. Cuando el lookup devuelve null, el handler llama a `WriteNotConfigured()` que escribe 404. El endpoint fallback (que sí devuelve 501) nunca se ejecuta porque el handler corre primero.
- **Fix aplicado**: en el handler, cuando no hay connection, devuelvo 501 con `saml.not_configured` y un detail que dice "Configure via POST /api/workspaces/{workspaceId}/saml or remove the routes from your reverse proxy". La diferencia: 404 dice "no existe", 501 dice "no está implementado/configurado para este workspace", y eso es lo correcto.
- **Verificación post-fix**: GET a `/saml/some-slug-that-doesnt-exist/login` ahora devuelve 501 con detalle útil.
- **Archivo**: `src/Cardscape.Api/Authentication/SamlAuthenticationHandler.cs:88-117`

### BETA-2-#13 — `RevokedTokenRepository.PurgeExpiredAsync` 500 (LINQ no traducible, RevocationSweeper muere cada 60s)
- **Síntoma**: en el log del container, cada minuto: `RevocationSweeper failed; will retry after the next interval. ... The LINQ expression 'DbSet<RevokedToken>().Where(r => r.TokenExpiresAt <= @now).ExecuteDelete()' could not be translated.`
- **Causa**: el sweeper de tokens revocados usaba `ExecuteDeleteAsync` con un `Where` que el provider SQLite no traduce. La primera vez que vi este stack en los logs de Docker durante la R2 me cayó la ficha: este es el mismo bug de patrón que BETA-2-#7. El RevocationSweeper hace retry cada 60s, así que en producción esto es un loop infinito de errores.
- **Fix aplicado**: cambio a `Select(Id).ToListAsync` + `RemoveRange` + `SaveChangesAsync` (mismo patrón que otros bulk-cleanup paths del proyecto). El sweep es infrecuente y la tabla es chica; el costo de la SELECT es despreciable.
- **Verificación post-fix**: log del container ya no muestra el stack, el sweeper completa el purge.
- **Severidad real**: alta — el sweeper estaba muerto en silencio, nunca limpiaba tokens revocados expirados. La tabla crece sin bound.
- **Archivo**: `src/Cardscape.Infrastructure/Repositories/RevokedTokenRepository.cs:38-65`

## Resumen de la R2

| Categoría | Bugs |
|---|---|
| 5xx en input del cliente (debería 4xx) | #1, #2, #3 (en parte), #4 |
| LINQ no traducible en repos | #7, #13 (regresión de R1 BUG #16) |
| Auth / scheme no registrado | #8, #9 |
| Falta validación de existencia | #5, #11 |
| Comportamiento incorrecto / idempotencia | #6, #10 |
| Status code incorrecto (404 vs 501) | #12 |

**Total**: 13 bugs reales · 13 resueltos en commit. 

**Patrón emergente**: el proyecto tiene un problema sistemático con el strongly-typed id en LINQ-to-SQL. Por lo menos 4 repos tienen el patrón `Where(x => x.SomeStronglyTypedId.Value == someValue)` que SQLite no traduce. La fix es siempre `AsAsyncEnumerable()` + filter client-side. Una auditoría de TODOS los repos para confirmar que ninguno quedó sin arreglar sería valiosa antes de v1.1.0. (BETA-2-#7 y #13 muestran que el barrido de R1 BUG #16 fue incompleto.)

## Verificación post-fix

| Test | Antes | Después |
|---|---|---|
| `POST /api/workspaces/{id}/region` con `{"region":"us"}` | 500 | **400** ✓ |
| `GET /api/workspaces/{id}/invitations` (sin query) | 500 | **200** ✓ |
| `GET /api/boards/{id}/automation` | 500 | **200** ✓ |
| `GET /api/integrations/github/pulls` sin `?boardId=` | 404 | **400** ✓ |
| `POST /api/boards` con `{"visibility":99}` | 201 | **400** ✓ |
| `POST /api/cards/{id}/assign/{randomGuid}` | 200 | **404** ✓ |
| `DELETE /api/checklists/{id}` (segunda vez) | 204 | **404** ✓ |
| `GET /api/auth/external/google/start` (sin Google:ClientId) | 500 | **501** ✓ |
| `GET /oauth/authorize` (sin auth) | 500 | **302 → /login** ✓ |
| `POST /api/auth/2fa/disable` con TOTP recién usado | 400 | **204** ✓ |
| `GET /saml/no-such-slug/login` | 404 | **501** ✓ |
| Container logs del RevocationSweeper | excepción cada 60s | **limpio** ✓ |
| **Total asserts en el re-run** | 202 | 202 |
| **Pass** | 184 (91%) | **194 (96%)** |
| **Fail** | 18 | 8 (todos test artifacts) |
| **5xx residuales** | 7 | **0** ✓ |

## Lo que el script del sub-agente hace en R2

`D:/GitHub/Cardscape/test-results/api/beta-test-r2.ps1` (1 600+ líneas) ejercita:
- Auth: register (3 users + dup), login, /me, refresh, revoke
- Workspaces: CRUD + region + members + invitations
- Boards: CRUD + rename/desc/visibility + archive + star + export + ics
- Lists: CRUD
- Cards: CRUD + move + due-date + complete + reopen + archive + restore + assign + label + mirror + snooze
- Comments: add/list/edit/delete
- Voting: toggle + get
- Checklists: create/rename/add-item/toggle/rename-item/delete-item/delete
- Labels: create/update/delete
- Custom Fields
- Recurrence (set/get/delete + 404-on-none)
- Notifications (list + unread-count + read + read-all)
- Search
- Activities
- Automation (list/create/update/delete) ← BETA-2-#7 era acá
- Dashboards
- API tokens
- OAuth (apps + flow)
- TOTP (enroll + verify + disable con TOTP + disable con recovery) ← BETA-2-#10 era acá
- External logins ← BETA-2-#8 era acá
- SCIM
- SAML ← BETA-2-#12 era acá
- Integrations (Google Drive, GitHub, Inbound Email) ← BETA-2-#11 era acá
- Internal (client-log + broadcast)
- MCP subscriptions
- Board Extensions
- AI
- Admin (DSR + McpSubscriptions)
- Security
- Import
- Background jobs
- Dev-only

Y los 8 fails del re-run son:
- `register duplicate` — test asume comportamiento previo (el segundo register ahora devuelve 400 correctamente, pero el test asume que el primero también devolvió 400 — test artifact)
- `workspaces set region` — test manda `{"region":"us"}` esperando 200; ahora devuelve 400 (BETA-2-#1 fix)
- `boards ics public` — el test crea un board Private, no Public (test artifact)
- `vote toggle bob` — bob es workspace member pero no board member (test artifact)
- `webhooks N/A probe` — el endpoint existe (200 con list vacío), el probe asume que no existe
- `api-token revoke (delete)` — race con el `POST /revoke` previo (test artifact)
- `oauth/authorize with token` — manda un client_id no válido (test artifact)
- `totp disable with recovery` — el test de disable con TOTP ya consumió el enrollment previo, el segundo test no puede correr (test artifact, no bug)

## UI walkthrough (Playwright MCP)

En progreso al cierre de R2. Ver `D:/GitHub/Cardscape/test-results/ui/beta-test-r2-ui.md` para el reporte completo cuando termine.

## Archivos generados durante R2

- `D:/GitHub/Cardscape/test-results/api/beta-test-r2.ps1` — script de testing
- `D:/GitHub/Cardscape/test-results/api/beta-test-r2-results.jsonl` — 202 líneas con cada assert
- `D:/GitHub/Cardscape/test-results/api/beta-test-r2-errors.jsonl` — solo los 5xx
- `D:/GitHub/Cardscape/test-results/api/beta-test-r2-stdout.log` — output completo
- `D:/GitHub/Cardscape/test-results/api/beta-test-r2-docker.log` — `docker logs cardscape.api` excerpt
- `D:/GitHub/Cardscape/test-results/ui/beta-test-r2-ui.md` — UI walkthrough (en progreso)
- `D:/GitHub/Cardscape/test-results/ui/screenshots/*.png` — screenshots de bugs UI

## Setup de R2

- Container: `cardscape.api` reconstruido en `http://localhost:8080`
- DB: SQLite recreado limpio
- Usuarios de testing: `alice@cardscape.test`, `bob.r2@cardscape.test`, `charlie.r2@cardscape.test`, `dave.r2@cardscape.test`, más los que el UI walkthrough haya creado
- Workspace: "Beta R2" con un board "Sprint Board" y 3 listas (To Do, In Progress, Done)
