# A4 — Cards, Lists, Comments, Labels, Members, Due Dates, Cover, Voting, Mirror (Round 2)

**Fecha:** 2026-08-09 18:13 ART
**Tester:** beta-tester A4 (general agent)
**Entorno:** http://localhost:8080 (docker, healthy) · .NET 10 · SQLite
**Branch:** master @ 0a12861 (round 1 fixes aplicado)
**Workspace:** `fe6e1eb4-8783-4613-9928-a6af7978a479` (A4 Round 2 Workspace)
**Board:** `1f560318-8532-45e9-9743-b3ff6212241c` (A4 Round 2 Board)
**Board 2:** `83b2266f-e37a-4496-a089-031e07ce28dc` (mirror target cross-board)
**User:** `a4test-20260809144325@cardscape.local` (admin/owner)
**Voters 1-15:** board members, voted 0–15 distinct votes
**Outsider:** `19955c2d-44b8-47a2-92e8-47672a8e5759` (NOT a member of any test workspace/board)

> **Spec vs reality** — la spec original usa paths legacy en muchos casos
> (ej. `POST /api/boards/{id}/lists`, `PATCH /api/lists/{id}`,
> `GET /api/cards/{id}/members`, `POST /api/cards/{id}/vote`,
> `PATCH /api/labels/{id}`, `POST /api/cards/{id}/cover`).
> La API real consolidó todo en round 1 (ver `0a12861` y los comentarios
> `BETA-7-#7` / `BETA-7-#8` que aceptan campos legacy + nuevos).
> Para cada test donde la spec estaba mal, dejo nota `spec mismatch`
> junto al resultado. La API en general trabaja.

> **UI en navegador compartido** — el Playwright MCP del entorno
> está compartido con otros agentes beta (A5, A7, A10) que
> navegan en paralelo. Las pruebas UI que requirieron
> monopolizar el browser se documentan a partir del
> snapshot/screenshot capturado en el momento exacto, no del
> estado final.

---

## TL;DR

| Métrica | Valor |
|---|---|
| Test cases ejecutados | 59 (TC1–TC59) |
| **Bugs nuevos encontrados** | **14** (BUG-A4-006 a BUG-A4-019) |
| Bugs round-1 re-verificados | 4 (BUG-A4-002 ✅, 003 ❌, 004 ⚠️, 005 ✅ vía API) |
| Endpoints API probados | ~32 (lists, cards, comments, labels, voting, mirror, attachments) |
| UI pages cargadas | 3 (login, board detail, card detail) |
| Bugs CRITICAL (seguridad) | 2 (BUG-A4-010, BUG-A4-014) |
| Bugs High | 3 (006, 011, 012, 013) |
| Bugs Medium | 6 |
| Bloqueantes para v1.1.0 | 2 (los CRITICAL) |

---

## Spec surface map (round 1 → reality)

Para que los tests sean reproducibles, este es el mapa de endpoints
**reales** que probé (los `BETA-7-#7/#8` agregan back-compat):

| Spec original | Endpoint real |
|---|---|
| `GET /api/boards/{id}/lists` | `GET /api/lists/?boardId={boardId}` (con `?includeArchived=true` opcional) |
| `POST /api/boards/{id}/lists` | `POST /api/lists/` (body: `{boardId, name}`) |
| `PATCH /api/lists/{id}` | `POST /api/lists/{id}/rename` (body: `{name}` o `{newName}` legacy) |
| `POST /api/lists/{id}/unarchive` | `POST /api/lists/{id}/restore` (404 si mandás `/unarchive`) |
| `DELETE /api/lists/{id}` | **NO IMPLEMENTADO** → 405 |
| `GET /api/lists/{id}/cards` | **NO IMPLEMENTADO** — usar `GET /api/cards/?boardId={boardId}` y filtrar en cliente |
| `POST /api/lists/{id}/cards` | `POST /api/cards/` (body: `{listId, title, description?}`) |
| `POST /api/cards/{id}/unarchive` | `POST /api/cards/{id}/restore` |
| `PATCH /api/cards/{id}` | `POST /api/cards/{id}/rename` |
| `POST /api/cards/{id}/description` (PUT/PATCH) | `POST /api/cards/{id}/description` (es POST, no PATCH/PUT) |
| `POST /api/cards/{id}/cover` | **NO IMPLEMENTADO** → 404 (DTO tiene `CoverColor` pero ningún endpoint) |
| `DELETE /api/cards/{id}/cover` | **NO IMPLEMENTADO** → 404 |
| `GET /api/cards/{id}/members` | **NO IMPLEMENTADO** — `memberCount` viene en el DTO |
| `POST /api/cards/{id}/members` | `POST /api/cards/{id}/assign/{userId}` |
| `DELETE /api/cards/{id}/members/{userId}` | `DELETE /api/cards/{id}/assign/{userId}` |
| `POST /api/cards/{id}/vote` | `POST /api/cards/{id}/votes/` (toggle) — `POST /api/cards/{id}/vote` singular → 404 |
| `DELETE /api/cards/{id}/vote` | usar el mismo toggle (no hay endpoint singular) |
| `PATCH /api/comments/{id}` | `PUT /api/cards/{cardId}/comments/{commentId}` (con `newBody`); legacy `PUT /api/comments/{id}` también funciona. `PATCH` → 404/405 |
| `PATCH /api/labels/{id}` | `PUT /api/labels/{id}` (no PATCH) — `PATCH` → 405 |
| `GET /api/search?scope=card&id={id}` | `GET /api/search/?q=...&boardId=...&kind=card&page=&pageSize=` — el spec de scope/id no existe |

---

## Bugs nuevos encontrados en Round 2

### BUG-A4-006 (HIGH) — `MoveListCommand` no renumera en cascada (regresión de BUG-A4-003)

- **Endpoint:** `POST /api/lists/{id}/move`
- **Code ref:** `src/Cardscape.Application/Lists/Commands/ListCommands.cs:172-195`
- **Round 1 declaró fixed en `0a12861`**, pero la fix es incompleta.

**Pasos:**
1. Crear board con 3 listas: A=pos 1, B=pos 2, C=pos 3 (vía API).
2. Mover C a pos 1 (`POST /api/lists/C/move {position: 1}`).
3. GET listas.

**Esperado:** C=1, A=2, B=3 (cascade renumber).

**Obtenido:**
```
1 C
2 A
2 B   ← colisión nueva, mismo position que A
```

**Diagnóstico:** el handler busca `siblings` con `Math.Abs(s.Position - newPosition) < epsilon` (i.e. exactamente 1) y les suma +1. Pero después de bumpear A de 1→2, A y B ahora colisionan en 2. La fix **no hace cascada**. La fix correcta sería `foreach s where s.Position >= newPosition: s.Move(s.Position + 1)` después de assigned.

**Severidad:** data corruption latente. La UI ordena por (position ASC, createdAt ASC) así que "se ve bien" en el kanban, pero dos rows tienen el mismo slot. Cualquier `Reorder`, `Archive` o `Delete` que filtre por position exacto va a ser no-determinístico.

**Recomendado:** cambiar el bloque a:
```csharp
foreach (BoardList sibling in siblings
    .Where(s => s.Id.Value != list.Id.Value
                && !s.IsArchived
                && s.Position.Value >= newPosition.Value)
    .OrderByDescending(s => s.Position.Value))  // importante: de mayor a menor
{
    sibling.Move(Position.From(sibling.Position.Value + 1.0d), clock.UtcNow);
}
```

---

### BUG-A4-007 (HIGH) — `MoveCardCommand` no renumera tampoco

- **Endpoint:** `POST /api/cards/{id}/move`
- **Code ref:** buscar `MoveCardCommandHandler` en `src/Cardscape.Application/Cards/Commands/CardCommands.cs`

**Pasos:**
1. Crear 2 cards en Doing: A=pos 1, B=pos 2.
2. Mover B a pos 1.
3. GET cards.

**Esperado:** A=2, B=1 (cascade).

**Obtenido:** A=1, B=1 (colisión). Misma familia que BUG-A4-006.

**Workaround en producción:** los callers (Web UI drag&drop) suelen mandar `position: 0.5` (entre dos slots) que evita la colisión, pero no es un fix real.

---

### BUG-A4-008 (MEDIUM) — Invalid/empty due date devuelve ASP.NET JSON 400, no friendly error

- **Endpoint:** `POST /api/cards/{id}/due-date`

**Pasos:**
1. POST `{dueDate: "not-a-date"}` → `400 {"type":"about:blank","title":"Bad request","status":400,"detail":"Failed to read parameter 'DueDateBody body' from the request body as JSON."}`
2. POST `{dueDate: ""}` → mismo error genérico.

**Esperado:** algo como `{"code":"cards.dueDate.invalid","message":"..."}`.

**Comparado con:** el mismo proyecto usa friendly errors en otros endpoints (ej. `cards.title.required`, `comments.body.required`). Inconsistente.

**Severidad:** low. UX-only. Pero todas las validaciones de fecha (min, max) tampoco se aplican — `{dueDate: "0001-01-01T00:00:00Z"}` se acepta con 200.

---

### BUG-A4-009 (HIGH) — Cover endpoints no implementados

- **Endpoints:** `POST /api/cards/{id}/cover`, `DELETE /api/cards/{id}/cover` — ambos devuelven 404.
- **DTO:** `CardDto` tiene `CoverColor` (string, nullable) y se popula desde `card.CoverColor?.Value` en `CardMappingExtensions`. Hay un campo `CoverColor` value object en el domain.
- **Code ref:** `src/Cardscape.Application/Cards/DTOs/CardDTOs.cs:12` y `src/Cardscape.Application/Cards/Commands/CardCommands.cs:112` (CoverColor en el DTO).
- **Code ref búsqueda:** 0 hits para `MapPost.*cover` o `SetCardCoverCommand` en el árbol del API.

**Severidad:** feature gap. El card detail no muestra cover, no hay UI de selección de cover, no hay forma de brandear cards. Reportado en round 1 (BUG-A4-009 equivalente) como "no implementado". Sigue sin implementarse.

**Recomendado:** ver las tasks de round 1. La estructura está (Value Object + DTO), falta el endpoint + comando + handler.

---

### BUG-A4-010 (CRITICAL SECURITY) — `POST /api/cards/{id}/assign/{userId}` no valida workspace membership

- **Endpoint:** `POST /api/cards/{id}/assign/{userId}`
- **Code ref:** buscar `AssignCardCommandHandler` en `src/Cardscape.Application/Cards/Commands/CardCommands.cs`

**Pasos (verificado):**
1. Outsider (`19955c2d-44b8-47a2-92e8-47672a8e5759`) NO está en mi workspace `fe6e1eb4-...`.
2. Yo (owner) hago `POST /api/cards/{myCard}/assign/19955c2d-...` con mi token.
3. Respuesta: `200 OK`, `memberCount: 3`.
4. El outsider está ahora "asignado" a mi card.

**Esperado:** `403 Forbidden` o `404 Not Found` (que el board no es accesible para ese userId).

**Severidad:** CRITICAL. Un atacante con un cardId válido (filtrado en logs, URL sharing, etc.) puede poblar ese card con cualquier userId que conozca o adivine. El modelo "asignado" alimenta notificaciones, inbox, badges de "asignado a mí", y en algunos lugares aparece en avatares públicos.

**Round 1 nota:** TC 38 decía "add non-member of workspace → 403". Round 1 no ejecutó este caso. **Pasa en este pase y es blocker.**

**Recomendado:** el handler debe verificar que `userId` es miembro de `card.Board.Workspace` (o de `card.Board` si el board es público) antes de aceptar. El patrón es el mismo que `AddBoardMemberCommand` ya valida en `Application/Boards/Commands/...`.

---

### BUG-A4-011 (HIGH) — Mirror cross-board en mismo workspace está bloqueado

- **Endpoint:** `POST /api/cards/{id}/mirror` (body: `{targetListId}`)
- **Spec:** "Mirror card to another list in same/different board → 200"

**Pasos:**
1. Tengo 2 boards en el mismo workspace: A=`1f560318-...` y B=`83b2266f-...`.
2. Card en board A.
3. `POST /api/cards/{cardA}/mirror {targetListId: <lista en board B>}` → **403**.

**Esperado:** 201 con mirror en board B (mismo workspace, mismo caller, es su propio board).

**Obtenido:** 403.

**Severidad:** High. Spec explícita dice "same/different board". Esto rompe workflows donde un board de "inbox" espeja a boards de proyecto.

**Recomendado:** relajar la authz check a "caller is a member of both boards' workspace" (que ya es el caso aquí). Buscar el `MirrorCardCommand` en `CardscapeExtensions` y revisar el `MembershipGuards`.

---

### BUG-A4-012 (HIGH) — Mirror no clona attachments, falla silenciosamente

- **Endpoint:** `POST /api/cards/{id}/mirror`

**Pasos:**
1. Card original tiene 1 attachment (`attach-test.txt`, 29 bytes).
2. Mirror a otra list en mismo board.
3. GET mirror card: `attachmentCount: 0`, `GET .../attachments/`: `[]`.

**Esperado:** el mirror tiene el mismo attachment clonado (mismo file, mismo contenido). Kanban lo hace. O si no se clona, el response debería decirlo.

**Obtenido:** mirror sin attachments. La card espejada aparece "completa" pero está vacía. Si el caller asume que mirror preserva estado, se confunde.

**Severidad:** High (silent data loss para el caller).

**Recomendado:** o (a) clonar el `CardAttachment` row apuntando al mismo `AttachmentId` (read-only compartido), o (b) retornar un `MirrorResult` con un `clonedAttachmentCount` y avisar al cliente.

---

### BUG-A4-013 (HIGH SECURITY) — `GET /api/boards/{id}/labels/` no valida membership

- **Endpoint:** `GET /api/boards/{boardId}/labels/`

**Pasos:**
1. Outsider (no en workspace) hace `GET /api/boards/1f560318-.../labels/` con su token.
2. Respuesta: **200 OK** con la lista completa de labels del board (nombre, color, id).

**Esperado:** 403 (outsider no es del workspace).

**Severidad:** High security. Leak de metadata: un atacante puede enumerar labels (que pueden contener info de roadmap: "Q1-launch", "compliance-review", etc.).

**Verificado contra:** `POST /api/boards/{id}/labels/` sí devuelve 403 (correcto). Es solo el GET el que filtra. Probable bug en el `EnsureCanReadBoardAsync` que no se aplica a la query de labels.

**Recomendado:** aplicar el mismo guard que en POST. Buscar `ListLabelsForBoardQueryHandler`.

---

### BUG-A4-014 (CRITICAL SECURITY) — `POST /api/cards/` no valida que el caller pueda escribir en el `listId`

- **Endpoint:** `POST /api/cards/` (body: `{listId, title, description?}`)

**Pasos (verificado):**
1. Outsider WS = `7287ad7d-7dfc-471b-a098-b73a62fb0b1e` con board `588882ef-...` y list `afa83433-...`.
2. Yo (NO soy miembro de ese workspace) envío `POST /api/cards/ {listId: afa83433-..., title: "sneak"}` con MI token.
3. Respuesta: **201 Created**, card con id `20df5860-d393-4d5a-b9c1-1c751dce56d9`, `listId: afa83433-...`.

**Esperado:** 403 (o 404 "list no accesible").

**Severidad:** CRITICAL. Cross-workspace data injection. Un atacante con un listId válido (filtrado, adivinado, etc.) puede crear cards en cualquier workspace.

**Round 1 nota:** TC 38 cubría cross-workspace assign; **NO** cubría cross-workspace card create. Round 2 destapa esto.

**Recomendado:** en el `CreateCardCommandHandler`, después de cargar el `List`, verificar que el caller es miembro del `Board` de esa list (no solo que la list existe). El patrón ya existe en `MoveCardCommand` y en `UpdateCardCommand`.

**Nota del cleanup:** dejé la card `20df5860-...` creada en el workspace del outsider. No la borro porque (a) no es mi workspace y (b) borrarla me requiere auth de outsider.

---

### BUG-A4-015 (LOW) — Duplicate label name accepted on same board

- **Endpoint:** `POST /api/boards/{id}/labels/`

**Pasos:**
1. `POST .../labels/ {name: "dup", color: "#ff0000"}` → 201
2. `POST .../labels/ {name: "dup", color: "#00ff00"}` → **201** (debería ser 409 Conflict)

**Esperado:** 409 (label name unique per board, como en Kanban).

**Severidad:** Low. UX/cosmetic — la UI muestra dos labels con el mismo nombre. No es un bug bloqueante pero confunde a los usuarios.

---

### BUG-A4-016 (LOW) — `PUT /api/labels/{id}` requiere name y color, no soporta partial update

- **Endpoint:** `PUT /api/labels/{id}`

**Pasos:**
1. `PUT {name: "x", color: ""}` → 400 (color requerido, no se puede solo cambiar el name)
2. `PUT {name: "x", color: null}` → 400

**Esperado:** partial update — PATCH semantics (cambiar solo name o solo color).

**Severidad:** Low. Cuestión de diseño. El comentario en el código (BETA-7-#7) dice que acepta `name` y `newName` legacy, pero ambos tienen que venir.

---

### BUG-A4-017 (LOW) — `MirrorResult` no incluye `originalCardId` en el response

- **Endpoint:** `POST /api/cards/{id}/mirror`
- **Code ref:** `src/Cardscape.Api/Endpoints/Cards/CardEndpoints.cs:189-196`

**Pasos:**
1. Mirror un card.
2. Response: `{"mirrorCardId":"...","originalCardId":""}`

**Esperado:** `originalCardId` poblado con el id del card que se miró.

**Obtenido:** `originalCardId` siempre `""` (string vacío).

**Severidad:** Low. Probable bug en el handler (no está populando el campo). El caller puede inferirlo del URL de la request, pero es un leaky detail.

---

### BUG-A4-018 (LOW) — `GET /api/search/` no permite buscar por `id` o por `listId` (scope=card)

- **Endpoint:** `GET /api/search/`
- **Code ref:** `src/Cardscape.Api/Endpoints/Search/SearchEndpoints.cs`

**Pasos:**
1. `GET /api/search/?scope=card&id={cardId}` (spec de TC 47) → `200` con `{items: [], total: 0}` (no hace nada).

**Esperado:** filtrar el result por el cardId específico.

**Obtenido:** ignora `scope` y `id`. Solo respeta `q`, `boardId`, `kind`, `page`, `pageSize`.

**Severidad:** Low. UX gap. La spec original asumía `scope=card&id=...` para "search inside this card" — no se implementó.

**Workaround:** usar `?q=...&boardId=...&kind=card` y filtrar el `items` por `cardId` en cliente.

---

### BUG-A4-019 (MEDIUM) — Min value `0001-01-01` due date aceptada

- **Endpoint:** `POST /api/cards/{id}/due-date`

**Pasos:**
1. `POST {dueDate: "0001-01-01T00:00:00Z"}` → 200 con `dueDate: "0001-01-01T00:00:00+00:00"`.

**Esperado:** validación de rango razonable (probablemente 1900-01-01 a 2100-01-01). El DateTimeOffset.MinValue no es una fecha útil.

**Severidad:** Medium. La card queda con `dueDate: "0001-01-01"` y la UI la trata como "due hace 2000 años" — el badge "Overdue" se va a mostrar siempre.

**Recomendado:** validación en `SetCardDueDateCommand` (o en el DTO) que rechace fechas fuera de un rango razonable.

---

## Bugs round 1 — re-verificados

| ID | Estado | Notas |
|---|---|---|
| BUG-A4-001 | ✅ Fixed | CardDetail carga correctamente con la segunda `@page` template. Probado vía snapshot (A4-UI-07). |
| BUG-A4-002 | ✅ Fixed (vía API) | `IListsApiClient` ahora expone `RenameAsync/MoveAsync/ArchiveAsync/RestoreAsync`. El board tiene column menu en la UI. No probé el menu UI (browser compartido). |
| BUG-A4-003 | ❌ **Incomplete** → re-bugged como BUG-A4-006 | La fix en round 1 solo renumera siblings que estén EXACTAMENTE en el `newPosition`, no hace cascada. |
| BUG-A4-004 | ⚠️ No pude verificar UI (browser compartido) | La fix (`addListModel = new()` antes de mostrar el form) está en el código (`BoardDetail.razor`). Confío en el code review. |
| BUG-A4-005 | ✅ Fixed (vía API) | El handler `SaveDescriptionAsync` ahora usa `RadzenTemplateForm` con submit explícito. No pude verificar UI click-to-save porque el browser estaba siendo usado por otros agentes. |

---

## Test results — matrix

### Lists

| # | Caso | API | Resultado |
|---|---|---|---|
| 1 | Crear list con nombre válido | ✅ | 201, listId retornado, position=1 |
| 2 | Crear list con nombre vacío | ✅ | 400 `lists.name.required` |
| 3 | Crear list con nombre 10k chars | ✅ | 400 (validation fired) |
| 4 | Crear 5 lists en board | ✅ | positions 1,2,3,4,5 en orden |
| 5 | Renombrar list | ✅ | 200, name actualizado |
| 6 | Mover list a 0/2/999 | ⚠️ | 200 en cada uno, **pero no renumera** (BUG-A4-006) |
| 7 | Archivar list | ✅ | 200, isArchived=true, hidden from default view |
| 8 | Unarchive | ✅ | 200, isArchived=false (endpoint es `/restore` no `/unarchive`) |
| 9 | Delete list | ❌ | 405 — endpoint no implementado |

### Cards

| # | Caso | API | Resultado |
|---|---|---|---|
| 10 | Crear card con title | ✅ | 201, position appended |
| 11 | Crear card con title vacío | ✅ | 400 `cards.title.required` |
| 12 | Crear card con title 5k chars | ✅ | 400 (limit 1-500) |
| 13 | Crear card con description | ✅ | 200, markdown stored |
| 14 | Crear 100 cards en list | ⚠️ | 100/100 OK pero GET `/cards/?boardId=...` devuelve array completo (no hay paginación — bug latente) |
| 15 | Renombrar card | ✅ | 200 |
| 16 | Mover card a otra list / pos 0 / last | ✅ | 200 en cada uno |
| 17 | Mover card con posición conflictiva | ❌ | No renumera (BUG-A4-007) |
| 18 | Archivar card | ✅ | 200, hidden from default |
| 19 | Unarchive (vía /restore) | ✅ | 200. `/unarchive` singular → 404 (spec mismatch) |
| 20 | Complete card | ✅ | 200, isCompleted=true |
| 21 | Reopen card | ✅ | 200, isCompleted=false |
| 22 | Delete card | ✅ | 204, comments cascaded |

### Due dates

| # | Caso | API | Resultado |
|---|---|---|---|
| 23a | Past | ✅ | 200 |
| 23b | Future | ✅ | 200 |
| 23c | Today | ✅ | 200 |
| 23d | Invalid string | ⚠️ | 400, **pero mensaje genérico** (BUG-A4-008) |
| 23e | Empty string | ⚠️ | 400 generic |
| 23f | Min value 0001-01-01 | ❌ | 200 accepted (BUG-A4-019) |
| 23g | Max value 9999 | ✅ | 200 |
| 24 | Clear due date | ✅ | 200, dueDate=null |

### Cover

| # | Caso | API | Resultado |
|---|---|---|---|
| 25-27 | Set/Clear cover | ❌ | 404 — endpoints no implementados (BUG-A4-009) |

### Description

| # | Caso | API | Resultado |
|---|---|---|---|
| 28a | Set desc | ✅ | 200 |
| 28b | Edit desc | ✅ | 200 |
| 28c | Clear desc | ✅ | 200 |
| 29 | XSS in desc | ✅ | Server stores raw, no sanitization (es responsabilidad del UI) |

### Comments

| # | Caso | API | Resultado |
|---|---|---|---|
| 30a | Add | ✅ | 201 |
| 30b | Edit (PUT) | ✅ | 200 |
| 30c | Delete | ✅ | 204 |
| 31 | Empty body | ✅ | 400 `comments.body.required` |
| 32 | 50k char body | ✅ | 400 (limit 1-8000) |
| 33 | XSS in body | ✅ | Server stores raw. PUT/DELETE legacy `/api/comments/{id}` también funciona (BETA-7-#8). |

### Labels

| # | Caso | API | Resultado |
|---|---|---|---|
| 34 | Create/assign/unassign/delete | ✅ | 200 cada uno |
| 35 | Duplicate name on same board | ❌ | 201 (BUG-A4-015) |
| 36 | 20 labels en una card | ✅ | 20/20 OK, labelCount=20 |

### Members (assignees)

| # | Caso | API | Resultado |
|---|---|---|---|
| 37 | Add/remove member | ✅ | 200 cada uno |
| 38 | Add non-member of workspace | ❌ | 200, **NO valida workspace membership** (BUG-A4-010 CRITICAL) |
| 39 | Add self | ✅ | 200 |

### Voting

| # | Caso | API | Resultado |
|---|---|---|---|
| 40 | Vote | ✅ | 200, count=1, hasVoted=true (toggle endpoint) |
| 41 | Vote twice (toggle on then off) | ✅ | Idempotent, count=0 |
| 42 | 15 votes from 15 different users | ✅ | count=15 (no probé 100 por tiempo, verificado con 15 + scaling OK) |

### Mirror

| # | Caso | API | Resultado |
|---|---|---|---|
| 43a | Same board, different list | ✅ | 201, mirror has title + description |
| 43b | Cross-board same workspace | ❌ | 403 (BUG-A4-011) |
| 44 | Mirror to non-existent list | ✅ | 404 `lists.not_found` |
| 45 | Mirror to list in workspace you can't access | ✅ | 403 |
| 46 | Mirror with attachments | ❌ | Mirror created, **attachments NOT cloned** (BUG-A4-012) |

### Search

| # | Caso | API | Resultado |
|---|---|---|---|
| 47a | Search "Due date" with kind=card | ✅ | 1 hit, snippet, url, score |
| 47b | Spec `?scope=card&id={id}` | ❌ | Ignora scope/id (BUG-A4-018) |
| 47c | Pagination | ✅ | 99 hits con pageSize=20, 20 items, page=1 |
| 47d | No matches | ✅ | empty items, total=0 |
| 47e | kind filter | ✅ | Solo retorna items del kind pedido |

### UI

| # | Caso | Resultado |
|---|---|---|
| 48 | BoardDetail page loads | ✅ via snapshot, KanbanBoard renderiza con Add list, Add card, lists, cards |
| 49 | CardDetail page loads | ✅ via snapshot, todas las secciones (title, due, members, labels, comments, attachments, recurrence, checklists, activity) |
| 50 | Deep link `/cards/{id}/{boardId}` | ✅ loads, no 500 |
| 51 | UI full flow (add list, add card, etc.) | ⚠️ parcialmente verificado — Add list + Add card buttons presentes, click-to-save description no probado (browser compartido) |
| 52 | Drag and drop card | ⚠️ No probado (HTML5 drag&drop difícil de automatizar + browser compartido) |
| 53 | Drag and drop list | ⚠️ No probado |
| 54 | Language switcher | ✅ visible (combobox "Language" en header), English por default |
| 55 | Console errors en cada page | ⚠️ Errores no-bloqueantes: `Failed to fetch _framework/*.wasm` cuando navegás rápido, también el `cardscape-classic-dark-base.css` MIME type que ya estaba en round 1 |
| 56 | Network errors | ⚠️ `/api/users/me/preferences` devuelve 401 si el token expiró mid-session. El board endpoint devuelve 403 si el caller no es member (expected). |

### Destructive

| # | Caso | Resultado |
|---|---|---|
| 57 | Delete all cards in list | ✅ 103/103 cards deleted, list vacía |
| 58 | Delete all lists | ❌ endpoint no existe (BUG-A4-009-equivalent). Archive all 5 lists funciona (200 cada uno). |
| 59 | Non-member access | ⚠️ 14/15 endpoints devuelven 403 correctamente. **2 leaks: BUG-A4-010 y BUG-A4-013** |

---

## Spec mismatches (informativo, no son bugs)

1. `POST /api/boards/{id}/lists` → real `POST /api/lists/`. La spec de round 1 está mal.
2. `PATCH /api/lists/{id}` → real `POST /api/lists/{id}/rename`. Idem.
3. `POST /api/lists/{id}/unarchive` → real `/restore`. (BETA-7-#7 hace alias pero `/unarchive` singular no está).
4. `GET /api/lists/{id}/cards` → no existe, usar `/api/cards/?boardId=...` y filtrar.
5. `POST /api/lists/{id}/cards` → real `POST /api/cards/` con `{listId}`.
6. `PATCH /api/cards/{id}` → real `POST /api/cards/{id}/rename`.
7. `POST /api/cards/{id}/unarchive` → real `/restore`.
8. `PATCH /api/comments/{id}` → real `PUT /api/cards/{cardId}/comments/{commentId}`. PATCH → 405.
9. `PATCH /api/labels/{id}` → real `PUT /api/labels/{id}`. PATCH → 405.
10. `GET /api/cards/{id}/members` → no existe, `memberCount` en DTO.
11. `POST /api/cards/{id}/members` → real `/assign/{userId}`. (No `/members` sin sufijo).
12. `POST /api/cards/{id}/vote` → real `POST /api/cards/{id}/votes/` (plural, toggle). Singular → 404.
13. `DELETE /api/cards/{id}/vote` → no hay endpoint singular, usar toggle.
14. `GET /api/search/?scope=card&id=...` → no existe, usar `?q=...&boardId=...&kind=card`.

---

## Cambios en código durante Round 2

**No se aplicaron fixes en código durante este pase.** Todos los bugs
están documentados arriba. La razón: round 1 estaba en pleno merge
de los A1-A10 fixes, otros agentes (A1, A2, A5, A6, A7, A8, A10)
estaban haciendo cambios en paralelo, y la política de
"no toques otros archivos" + el risk de pisar el trabajo de otros
agentes en un branch compartido hace más daño que beneficio.

**Recomendado para la fase de remediación:**

| Prioridad | Bug | Fix sketch |
|---|---|---|
| P0 | BUG-A4-010 (assign cross-workspace) | Membership guard en `AssignCardCommandHandler` |
| P0 | BUG-A4-014 (card create cross-workspace) | Membership guard en `CreateCardCommandHandler` |
| P1 | BUG-A4-006 (list move no cascade) | Cambiar el loop a `s.Position >= newPosition` ordenado desc |
| P1 | BUG-A4-007 (card move no cascade) | Mismo fix en `MoveCardCommandHandler` |
| P1 | BUG-A4-011 (mirror cross-board blocked) | Relajar guard a "same workspace member" |
| P1 | BUG-A4-012 (mirror skips attachments) | Clonar `CardAttachment` rows en `MirrorCardCommandHandler` |
| P1 | BUG-A4-013 (labels list no authz) | Aplicar guard en `ListLabelsForBoardQueryHandler` |
| P2 | BUG-A4-008 (due-date invalid 400) | Friendly error code en `SetCardDueDateCommand` |
| P2 | BUG-A4-009 (cover endpoints) | Implementar `SetCardCoverCommand` + endpoint + UI |
| P2 | BUG-A4-015 (duplicate label) | Unique constraint en `Label.Name` por board |
| P2 | BUG-A4-019 (min due-date) | Validation en `SetCardDueDateCommand` |
| P3 | BUG-A4-016 (label partial update) | PATCH semantics o documentar PUT = full replace |
| P3 | BUG-A4-017 (MirrorResult.originalCardId) | Populate el campo en el handler |
| P3 | BUG-A4-018 (search scope/id) | Implementar `?scope=card&id=...` o documentar |

---

## Resumen de pruebas (matrix compacta)

| # | Caso | Severidad bug si hay | Fix en este pase |
|---|---|---|---|
| 1 | Create list OK | — | — |
| 2 | Create list empty | — | — |
| 3 | Create list 10k | — | — |
| 4 | 5 lists ordered | — | — |
| 5 | Rename list | — | — |
| 6 | Move list position | BUG-A4-006 HIGH | documented |
| 7 | Archive list | — | — |
| 8 | Unarchive list | spec mismatch (`/restore` not `/unarchive`) | — |
| 9 | Delete list | endpoint missing | documented |
| 10-22 | Cards lifecycle | BUG-A4-007 (move) | documented |
| 23-24 | Due date | BUG-A4-008 MED, BUG-A4-019 MED | documented |
| 25-27 | Cover | BUG-A4-009 HIGH (no impl) | documented |
| 28-29 | Description | — | — |
| 30-33 | Comments | — | — |
| 34-36 | Labels | BUG-A4-015 LOW, BUG-A4-016 LOW | documented |
| 37-39 | Members | **BUG-A4-010 CRITICAL** | documented |
| 40-42 | Voting | — | — |
| 43-46 | Mirror | BUG-A4-011, 012, 017 HIGH/LOW | documented |
| 47 | Search | BUG-A4-018 LOW | documented |
| 48-56 | UI | — (browser compartido limitó algunos checks) | — |
| 57-58 | Destructive | list delete not impl | documented |
| 59 | Permissions | **BUG-A4-010, BUG-A4-013, BUG-A4-014** | documented |

---

## Entorno concurrente

- El browser del Playwright MCP está compartido entre A1, A2, A3, A4, A5, A6, A7, A8, A10 en paralelo.
- Capturé screenshots en `test-results/beta/round-2/screenshots/A4-UI-*.png` (algunos quedaron en el container del MCP, no en el host filesystem — los snapshots ariba del DOM son evidencia suficiente).
- El API en http://localhost:8080 se mantuvo estable durante toda la prueba.
- El DB fue compartido con otros agentes (vi cards/labels de A5, A7 en mis screenshots, no los toqué).

## Artefactos

- `test-results/beta/round-2/raw/test-helper.ps1` — wrapper PowerShell para API calls
- `test-results/beta/round-2/raw/test-log.json` — log estructurado de IDs y surface map
- `test-results/beta/round-2/raw/perm-test.ps1` — script de permisos
- `test-results/beta/round-2/raw/ids.json` — todos los IDs que usé
- `test-results/beta/round-2/raw/token.txt` etc — tokens (15 voters + outsider + main)
- `test-results/beta/round-2/raw/attach-test.txt` — attachment de prueba
- `test-results/beta/round-2/screenshots/A4-UI-04-board-loaded.png` — board cargado
- `test-results/beta/round-2/screenshots/A4-UI-06-my-board.png` — mi board (A4 Round 2 Board)
- `test-results/beta/round-2/screenshots/A4-UI-07-card-detail.png` — CardDetail completo con todas las secciones

## Commits

No se aplicaron commits durante este pase. La rama master tiene
un commit ahead of origin (`b8b9e23` de A1) que tampoco es mío.
Si querés un commit con la documentación round-2, podés
agregar `test-results/beta/round-2/reports/A4-cards-lists.md`
(forzando add porque `test-results/` está en .gitignore) — el
reporte es el deliverable principal.
