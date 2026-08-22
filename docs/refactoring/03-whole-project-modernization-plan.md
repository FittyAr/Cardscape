# Plan de modernización integral de Cardscape

> **Estado**: En ejecución  
> **Inicio**: 2026-08-11  
> **Stack objetivo**: .NET 10, ASP.NET Core 10, Blazor WebAssembly 10, EF Core 10 y Radzen.Blazor  
> **Rama de entrega**: `master` (rama principal real del repositorio; `origin/HEAD` apunta a `origin/master`)  
> **Compatibilidad**: no se preservará compatibilidad hacia atrás mientras el producto no esté en producción.

## 1. Objetivo y reglas de decisión

El objetivo es llevar todo el repositorio a un estándar profesional, verificable y mantenible. La revisión cubre arquitectura, dominio, casos de uso, persistencia, API, MCP, Blazor, seguridad, observabilidad, pruebas, automatización y documentación.

Reglas permanentes:

- Favorecer APIs y patrones modernos soportados por .NET 10; no introducir previews ni actualizar `global.json` sin autorización explícita.
- Mantener Clean Architecture sólo donde los límites aporten independencia real. Eliminar abstracciones, service locators y capas ceremoniales que no protejan una frontera.
- Organizar Application/API/Web por capacidad o bounded context y evitar archivos monolíticos.
- Desarrollar y ejecutar la suite ordinaria sobre SQLite, manteniendo compatibilidad de release comprobada con PostgreSQL y MySQL.
- No anunciar MariaDB ni publicar una release que lo incluya hasta que un provider EF Core 10 estable supere migraciones e integración sobre MariaDB real. Una declaración de compatibilidad sin matriz verde no satisface este gate.
- Usar exclusivamente componentes Radzen en la UI. CSS isolation se acepta sólo para interacciones sin equivalente Radzen, como un kanban; cualquier otra excepción deberá quedar justificada.
- No mantener rutas, contratos, migraciones o adaptadores obsoletos por compatibilidad. Los cambios incompatibles deben actualizar en el mismo bloque código, pruebas y documentación.
- Cada bloque termina con build Release sin warnings, pruebas pertinentes verdes, actualización de este checklist, commit pequeño y push a `origin/master`.

## 2. Línea base y hallazgos iniciales

### 2.1 Estado comprobado

- [x] SDK fijado y disponible: .NET SDK `10.0.302`; proyectos de producto en `net10.0`.
- [x] Gestión central de paquetes mediante `Directory.Packages.props`.
- [x] Warnings tratados como errores y análisis Roslyn habilitado.
- [x] Proyectos principales separados en Domain, Application, Infrastructure, Api, Web y Mcp.
- [x] Pruebas de arquitectura existentes: 10/10 verdes.
- [x] Build Release: 0 warnings y 0 errores al deshabilitar el compilador compartido bloqueado por el entorno.
- [x] Línea base de tests ejecutada: 707 pass, 5 E2E fail, 1 skip.
- [x] Causa de las 5 E2E: `IMessageBus` scoped resuelto desde el provider raíz y almacenado en estado global.
- [x] Corrección aplicada: inyección DI normal en MCP; service locator estático eliminado; 5/5 E2E verdes.

### 2.2 Riesgos y deuda confirmados

- [x] `Cardscape.Seeder` era una dependencia de API pero no figuraba en `Cardscape.slnx`; esto permitió que un build Release de la solución lo produjera en Debug.
- [x] La documentación normativa principal fue reconciliada con Wolverine, .NET 10 y las versiones centrales instaladas. Los ADR y auditorías históricas se preservan sin reescritura.
- [ ] `Directory.Build.props` suprime una lista extraordinariamente amplia y duplicada de analizadores. Esto reduce el valor de `TreatWarningsAsErrors`; debe reducirse gradualmente con justificación por regla.
- [ ] Existen archivos con demasiadas responsabilidades: `CardDetail.razor` (~58 KB), `BoardDetail.razor` (~34 KB), el registro DI de Infrastructure (~32 KB), `BoardsTools.cs` (~31 KB) y `Api/Program.cs` (~18 KB). El antiguo `CardCommands.cs` de 1.114 líneas ya fue eliminado: sus 16 casos de uso y mapping se distribuyen por mutaciones, planificación, ciclo de vida, relaciones y mapeo, con un máximo de 344 líneas por archivo.
- [x] SQLite, PostgreSQL y MySQL tienen assemblies de migración EF Core separados; CI aplica las historias externas sobre PostgreSQL 17 y MySQL 8.4 reales y el job bloquea releases. MariaDB queda explícitamente fuera hasta disponer de provider EF Core 10 estable.
- [ ] Hay comentarios y descripciones de proyecto que siguen llamando “scaffold/placeholder” a código activo; pueden ocultar funcionalidad incompleta real.
- [ ] La solución contiene documentación histórica extensa y contradictoria con el estado actual. Los ADR se preservan, pero la documentación normativa debe reconciliarse.
- [x] Las rutas destructivas del Seeder estaban expuestas con `AllowAnonymous`; el grupo completo ahora exige `AdminOnly` y tiene cobertura 401/403/admin.
- [x] Retention y Revocation aceptaban configuración inválida hasta fallar dentro de hosted services; ahora validan al arrancar.
- [x] `CardsPerBoard` y `UserCount` eran opciones ficticias del Seeder sin consumidores reales; fueron eliminadas de configuración, API y UI.
- [x] `IRetentionSettings` era una abstracción propiedad de Infrastructure que sólo duplicaba `IOptions<T>`; se eliminó junto con su adaptador y se corrigió la regla arquitectónica que no podía detectarla.
- [x] Seeder publicaba su pipeline interno completo y un provider de una sola propiedad; los pasos ahora son internos, el reporte se inyecta directamente y una regla protege la superficie pública.
- [x] `IMcpResourceNotifier` filtraba una integración HTTP API→MCP dentro de Application sin ningún consumidor interno; el notifier concreto ahora pertenece por completo al host API.
- [x] `IPendingTotpLoginStore` se confirmó como puerto real con backends memoria/Redis; su implementación en memoria ahora usa el reloj inyectado y permite validar exactamente el TTL.
- [x] El puerto calendario tenía el nombre ambiguo `IIcalendarService` y generaba `DTSTAMP` con tiempo global; ahora expresa la capacidad `ICalendarFeedRenderer` y usa `IClock`.
- [x] MCP duplicaba `CurrentUser` y no registraba su accessor real; ahora reutiliza el mapping de Application, registra sólo el adaptador de transporte y E2E ya no parchea la composición.
- [x] Los API tokens MCP emitían scopes pero ninguna herramienta los consumía; un filtro central ahora exige `read` o `write`, deniega herramientas sin clasificar y un invariant mantiene completo el catálogo.
- [x] Recursos, prompts, completion y suscripciones MCP omitían scopes; las ocho superficies de datos ahora comparten la autorización exacta `read` antes de ejecutar handlers.
- [x] Suscripciones MCP ahora normalizan el URI, validan membresía al crearse, conservan identidad sólo internamente y la revalidan antes de cada fan-out.

## 3. Plan de ejecución

### Fase 0 — Higiene y línea base

- [x] Inventariar repositorio, proyectos, dependencias, ramas e instrucciones.
- [x] Leer README, ADR y contrato operativo.
- [x] Ejecutar build Release y suite completa.
- [x] Corregir el fallo de arranque MCP que rompía toda la suite E2E.
- [x] Incluir `Cardscape.Seeder` explícitamente en la solución.
- [x] Ejecutar nuevamente la suite completa: 712 pass, 0 fail, 1 skip.
- [x] Confirmar que `.env` está ignorado y no está versionado.

### Fase 1 — Arquitectura y estructura

- [x] Generar el grafo efectivo de referencias entre proyectos y reforzar sus invariantes con architecture tests.
- [x] Revisar ubicación y dependencia de cada abstracción; Domain no depende de frameworks y Application sólo referencia Domain. Todos los puertos públicos de Application viven ahora bajo `Cardscape.Application.Abstractions`: calendario, TOTP pendiente y realtime dejaron sus namespaces de feature sin conservar aliases. Un invariant inspecciona el assembly completo y bloquea nuevas abstracciones públicas fuera de ese límite.
- [x] Auditar composición DI de API, MCP y Seeder: lifetimes, duplicación, validación al arranque y opciones tipadas.
- [ ] Revisar boundaries y vertical slices; dividir archivos monolíticos por caso de uso sin crear capas adicionales. El mirror de tarjetas ya tiene un único comando canónico compartido por REST/MCP; se eliminó el handler stub que no creaba la tarjeta destino. Los comandos Card dejaron el monolito de 1.114 líneas y ahora se agrupan en cinco archivos cohesionados. Checklists dejó su archivo único de 647 líneas y separa contratos/query, ciclo de vida, edición de ítems y cambios de estado. Ambos conservan namespace, contratos y discovery de Wolverine; continúan los monolitos restantes.
- [ ] Revisar el rol del SDK público y evitar duplicación de contratos con Web/API.
- [ ] Alinear solución, Docker, CI, scripts y documentación con el mismo conjunto de proyectos.
- [x] Reconciliar documentación normativa con Wolverine, .NET 10 y versiones instaladas.

### Fase 2 — Superficies críticas

- [ ] Autenticación/autorización: JWT, API tokens, OAuth/OIDC, SAML, SCIM, 2FA, políticas y aislamiento multi-tenant. La autorización admin falla cerrada, la administración SAML completa es owner-only, la sesión refresh ficticia fue eliminada y `exp` es la única expiración JWT. Google, Microsoft y Apple usan correlación protegida más cookie externa efímera; SCIM está aislado por owner/workspace; `RequireTwoFactor` bloquea JWT y sólo acepta credenciales TOTP confirmadas. El enrolamiento queda pendiente hasta probar el autenticador; Slack valida owner/workspace y GitHub exige un enlace repo-board activo para toda lectura o escritura externa. La descarga de metadata SAML revalida SSRF, no sigue redirects, tiene timeout y límite de 1 MiB; se eliminó el acceso `file://` inalcanzable. Quedan los invariantes multi-tenant restantes.
- [ ] Persistencia: modelo EF, transacciones, concurrencia, índices, consultas N+1, tracking y compatibilidad de los tres providers. Todos los repositorios, la exportación personal y el wipe del Seeder ya filtran, ordenan, paginan, agregan o eliminan mediante LINQ/EF Core; mirrors dejó de ejecutar N+1 y las mutaciones masivas usan `ExecuteUpdateAsync`/`ExecuteDeleteAsync`. No queda SQL manual en `src`. Los índices de actividad, jobs, notificaciones, invitaciones, tokens, automatizaciones, custom fields, labels y colecciones de card ya siguen sus filtros/órdenes reales y se retiraron prefijos redundantes. El modelo común dejó de forzar 19 tipos `TEXT` propios de SQLite y permite que cada provider elija su tipo nativo. `RowVersion` ahora es una convención única para toda entidad y owned type; el interceptor evita dobles incrementos cuando el dominio ya selló el cambio, y los tres historiales corrigen los defaults omitidos. Sólo SQLite conserva en cliente comparaciones/orden `DateTimeOffset` no traducibles, SCIM verifica localmente hashes con sal tras filtrar activos mediante EF Core y los tokens CSV mantienen su coincidencia exacta tras un prefiltro EF Core. Continúa la auditoría de transacciones y consistencia de eventos.
- [ ] Gestión de secretos, cifrado, datos personales, borrado/anominización y retención. Webhooks y Slack ya persisten ciphertext con Data Protection; Slack dejó de ignorar el token por workspace y eliminó el fallback global multi-tenant incorrecto. Las invitaciones ya no envían el token a un transporte de email simulado que lo registraba en logs; la UI mantiene entrega manual de una sola visualización.
- [ ] Webhooks, importaciones, adjuntos y clientes HTTP: SSRF, validación, límites, reintentos, timeouts e idempotencia. Google Calendar ya no anuncia watch/webhook/pull ficticios; conserva únicamente el push saliente. Sus clientes OAuth/Calendar no siguen redirects, tienen timeouts y límites de respuesta, no reflejan cuerpos del proveedor y conservan el mapping card-event por conexión para ejecutar POST/PUT/DELETE reales. Google Drive fue eliminado por completo. Las rutas jerárquicas de adjuntos y webhooks ya exigen coincidencia entre URL y recurso persistido. Kanban separa preview/apply explícitamente, mantiene paridad de resumen y conserva asociaciones card-label. Los webhooks protegen el secreto de firma con Data Protection, firman con el secreto real, no revelan prefijos derivados, no siguen redirecciones y limitan la lectura de errores.
- [ ] Wolverine/background jobs: scopes, retries, outbox/inbox, cancelación y consistencia de eventos. Claim multi-worker ya es atómico. Los eventos de dominio ahora crean en la misma transacción una entrega outbox por broadcaster; leases EF Core evitan doble claim, los fallos se aíslan y reintentan con backoff, y un hosted dispatcher recupera commits interrumpidos. Continúan inbox para mensajes externos y la auditoría integral de cancelación.
- [x] MCP: autorización equivalente a REST, lifetimes, transporte, suscripciones e idempotencia. Scopes, lifetime/composición, Streamable HTTP autenticado, aislamiento de suscripciones y reservas idempotentes cross-process verificados.
- [ ] Observabilidad: logs estructurados, correlación, trazas, métricas, health checks y ausencia de PII/secrets. Se eliminó el `DatabaseLogSink` placeholder que descartaba eventos; sólo se anuncian console, rolling JSON y OTLP funcionales.

### Fase 3 — API y contratos

- [ ] Revisar semántica HTTP, Problem Details, validación, cancelación y códigos de estado de todos los endpoints.
- [ ] Eliminar endpoints legacy y contratos duplicados porque no se exige retrocompatibilidad. Retirados aliases `new*` de mutaciones Board/List/Card, rutas planas legacy de Comments, `/auth/logout`, `members_assign`, la ruta corta de Google Calendar, enums numéricos en JSON/rutas/query, el comando/store `AddAsync` de idempotencia, el fallback admin para JWT antiguos y cuatro mappings SAML inalcanzables; continúa la auditoría del resto de la API.
- [ ] Verificar OpenAPI/Scalar y sincronía con SDK/Web.
- [ ] Normalizar paginación, filtros, límites y errores.

### Fase 4 — Blazor WebAssembly y UI Radzen

- [ ] Leer la skill local `radzen-blazor` antes del primer cambio UI.
- [ ] Auditar todas las páginas por componente, estado, accesibilidad, responsive, loading/empty/error y navegación.
- [ ] Dividir `CardDetail.razor` y `BoardDetail.razor` en componentes cohesionados y testeables.
- [ ] Eliminar HTML/CSS/JS custom no autorizado cuando Radzen ofrezca equivalente.
- [ ] Revisar formularios, validadores, dialogs, grids, virtualización y renderizado para evitar trabajo innecesario.
- [ ] Validar temas, contraste, teclado, foco y localización.

### Fase 5 — Calidad y pruebas

- [ ] Auditar calidad de assertions, anti-patterns, gaps y cobertura de rutas críticas.
- [ ] Incorporar tests sólo mediante el pipeline `code-testing-agent` exigido por el repositorio.
- [ ] Resolver el test omitido o documentar técnicamente por qué no puede ejecutarse.
- [ ] Reducir supresiones globales de analyzers y mover excepciones inevitables al scope mínimo.
- [ ] Añadir validaciones de arquitectura para los defectos encontrados (lifetimes cuando sea comprobable, referencias y convenciones).
- [ ] Revisar tests funcionales/E2E para que validen comportamiento y no detalles internos.

### Fase 6 — Operación y cierre

- [ ] Auditar Docker/Compose, configuración por ambiente, health checks, graceful shutdown y despliegue reproducible.
- [ ] Revisar CI, supply chain, dependencias vulnerables y actualizaciones compatibles con .NET 10.
- [ ] Consolidar documentación operativa y eliminar contradicciones sin reescribir ADR históricos.
- [ ] Ejecutar build, tests, análisis y smoke tests finales.
- [ ] Publicar informe final de arquitectura, deuda residual justificada y siguientes decisiones.

## 4. Registro de bloques entregados

| Fecha | Bloque | Resultado | Verificación | Commit |
|---|---|---|---|---|
| 2026-08-11 | Línea base + lifetime MCP | Eliminado `McpToolContext`; prompts, resources y AI tools usan DI scoped | Suite: 712 pass, 0 fail, 1 skip | `8d923fe` |
| 2026-08-11 | Estructura de solución | Seeder agregado explícitamente a `Cardscape.slnx` | Build Release: 0 warnings, 0 errors; Seeder en Release | `8d923fe` |
| 2026-08-11 | Grafo + background jobs | ProjectReference validado desde MSBuild; registry inmutable construido por DI; documentación normativa reconciliada | Build 0/0; suite 721 pass, 0 fail, 1 skip | `2702468` |
| 2026-08-11 | Seeder + options | Seeder protegido con AdminOnly; opciones ficticias eliminadas; Retention/Revocation validan al arranque | Build 0/0; suite 735 pass, 0 fail, 1 skip | `af539f4` |
| 2026-08-11 | Ownership de abstracciones | Retention consume Options directamente; reloj inyectado consistente; regla contra interfaces públicas de Infrastructure corregida | Build 0/0; suite 735 pass, 0 fail, 1 skip | `adb2a8c` |
| 2026-08-11 | Superficie pública Seeder | Pipeline internalizado; provider ceremonial eliminado; construcción encapsulada; invariant de arquitectura agregado | Build 0/0; suite 736 pass, 0 fail, 1 skip | `6d8f5ac` |
| 2026-08-11 | Ownership realtime | Contrato API→MCP retirado de Application; notifier concreto encapsulado en API; whitelist arquitectónica de puertos realtime | Build 0/0; suite 737 pass, 0 fail, 1 skip | `2451611` |
| 2026-08-11 | Lifetime TOTP pendiente | Puerto preservado por tener dos backends; memoria usa IClock; expiración y single-use fijados con tests | Build 0/0; suite 743 pass, 0 fail, 1 skip | `e6cd876` |
| 2026-08-11 | Contrato calendario | Puerto renombrado por capacidad; DTSTAMP determinista mediante IClock; RFC 5545 fijado con tests | Build 0/0; suite 744 pass, 0 fail, 1 skip | `9b1cf16` |
| 2026-08-11 | Current user MCP | Mapping duplicado eliminado; accessor MCP registrado en producción; workaround E2E removido; invariant agregado | Build 0/0; suite 745 pass, 0 fail, 1 skip | `9052126` |
| 2026-08-11 | Scopes MCP | Filtro central deny-by-default; catálogo explícito read/write; invariant contra herramientas sin clasificar | Build 0/0; suite 757 pass, 0 fail, 1 skip | `5899713` |
| 2026-08-11 | Superficies de lectura MCP | Política reutilizable; recursos, prompts, completion y suscripciones exigen read; composición SDK fijada por test | Build 0/0; suite 763 pass, 0 fail, 1 skip | `57b6d04` |
| 2026-08-11 | Identidad en suscripciones MCP | URI board canónico; membresía validada al suscribir y antes de fan-out; identidad no expuesta | Build 0/0; suite 772 pass, 0 fail, 1 skip | `d1403d2` |
| 2026-08-11 | Contratos URI de recursos MCP | Parser compartido respeta autoridad/path de los cinco templates; suscripciones reutilizan el mismo contrato board | Build 0/0; suite 785 pass, 0 fail, 1 skip | `e93030e` |
| 2026-08-11 | Idempotencia global MCP | `_meta.idempotencyKey` se aplica en un filtro central a todo el catálogo write; hash canónico incluye herramienta y argumentos | Build 0/0; suite 796 pass, 0 fail, 1 skip | `142735a` |
| 2026-08-11 | Claim atómico de background jobs | Cada candidato usa UPDATE guardado por status + RowVersion; workers concurrentes reciben batches disjuntos | Build 0/0; suite 799 pass, 0 fail, 1 skip | `b1dfed7` |
| 2026-08-11 | Transporte e identidad MCP | stdio ficticio reemplazado por Streamable HTTP stateful autenticado; principal MCP propagado entre scopes del SDK | Build 0/0; suite 802 pass, 0 fail, 1 skip | `46dd323` |
| 2026-08-11 | Reservas idempotentes atómicas | REST y MCP reservan antes del efecto; contendientes cross-process esperan/reproducen; errores y leases permiten recuperación | Build 0/0; suite 808 pass, 0 fail, 1 skip | Pendiente |
| 2026-08-11 | Contratos preproducción canónicos | Eliminados aliases de mutaciones/comentarios, enums numéricos y superficies REST/MCP/Blazor duplicadas; SDK serializa con sus opciones configuradas | Build 0/0; suite 814 pass, 0 fail, 1 skip | `d136775` |
| 2026-08-11 | Autorización fail-closed + ownership SAML | JWT sin `is_admin` ya no usa fallback de BD; handler SAML es dueño único de las rutas de protocolo | Build 0/0; suite 814 pass, 0 fail, 1 skip | `ea54435` |
| 2026-08-11 | Sesión refresh ficticia | Eliminados endpoint, parser JWT sin validar, emisión opaca, DTOs, callbacks, placeholder OAuth y almacenamiento Web sin consumidor | Build 0/0; suite 815 pass, 0 fail, 1 skip | `8291cef` |
| 2026-08-11 | Expiración JWT canónica | Eliminado metadata duplicado; `exp` firmado usa configuración validada al arranque con límites seguros y ownership de secreto sólo en API | Build 0/0; suite 822 pass, 0 fail, 1 skip | Pendiente |
| 2026-08-11 | Límite OAuth/OIDC externo | Eliminado `state` casero sin validar; cookie externa efímera y correlación protegida del framework; callback Apple separado; proveedor y retorno local validados; SPA respeta el retorno | Build 0/0; suite 833 pass, 0 fail, 1 skip | Pendiente |
| 2026-08-11 | Aislamiento de credenciales SCIM | Issue/list/revoke owner-only; revoke exige coincidencia token-workspace; reloj y cancelación propagados en autenticación; `LastUsedAt` verificado | Build 0/0; suite 835 pass, 0 fail, 1 skip | Pendiente |
| 2026-08-11 | Enforcement 2FA por workspace | Política deja de ser decorativa: activación exige enrolamiento de todos los miembros; login niega JWT en estado inconsistente; `LastLogin` sólo se registra tras completar factores | Build 0/0; suite 838 pass, 0 fail, 1 skip | `7681a0c` |
| 2026-08-11 | Confirmación del enrolamiento TOTP | Alta pendiente hasta probar el autenticador; recovery codes bloqueados antes de activación; rotación segura del setup pendiente; flujo UI Radzen completo | Build 0/0; suite 843 pass, 0 fail, 1 skip | `c84b8c3` |
| 2026-08-12 | Aislamiento de administración SAML | Lectura, configuración y baja uniformemente owner-only; eliminado IDOR que exponía metadata IdP entre tenants | Build 0/0; suite 848 pass, 0 fail, 1 skip | `ebf6292` |
| 2026-08-12 | Límite workspace Slack | Connect/reconnect owner-only; rotación real y atómica de team/token; list/link/unlink rechazan route-resource mismatch en REST/MCP | Build 0/0; suite 852 pass, 0 fail, 1 skip | `7a51cc3` |
| 2026-08-12 | OAuth Google Calendar | Estado OAuth temporal protegido conserva usuario/workspace y sólo permite retorno local; eliminados REST de credenciales y watch/webhook/pull ficticios | Build 0/0; suite 853 pass, 0 fail, 1 skip | `be14a6f` |
| 2026-08-14 | Límite repo-board GitHub | Pulls/issues/PR links exigen repo activo del board; cliente y UI Radzen vuelven obligatorio `boardId` | Build 0/0; suite 854 pass, 0 fail, 1 skip | `5fbab91` |
| 2026-08-14 | Eliminación Google Drive ficticio | Retirados UI, REST, MCP, DI, dominio, persistencia, Seeder y schema de una integración sin callback y con attach no persistente | Build 0/0; suite 855 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Límites route-resource anidados | Download/delete de adjuntos y update/delete/deliveries de webhooks rechazan padres de URL ajenos; retirado `Events` ficticio de PATCH/OpenAPI | Build 0/0; suite 857 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Fidelidad importación Kanban | Rutas preview/apply explícitas; preview sin escrituras y con conteos reales; apply conserva `labelIds`; eliminada ruta booleana ambigua | Build 0/0; suite 859 pass, 0 fail, 1 skip | `6a3c382` |
| 2026-08-14 | Identidad propia sin referencias a competidores | Renombrados código, REST, MCP, UI, recursos, SDK, Seeder, tests, documentación y artefactos; formato JSON Kanban propio | Búsqueda 0 contenido/0 nombres; suite 859 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Seguridad de entrega webhook | Secreto reversible sólo mediante Data Protection, HMAC con secreto real, DTO sin prefijo, cliente nombrado sin redirects y cuerpo de error acotado | Build 0/0; suite 862 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Límite HTTP de metadata SAML | Eliminado `new HttpClient` y soporte `file://`; cliente nombrado sin redirects, revalidación SSRF, timeout y streaming limitado a 1 MiB | Build 0/0; suite 866 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Credencial Slack por workspace | Reemplazado hash no utilizable + token global por ciphertext Data Protection por tenant; envío usa sólo la credencial del workspace y DTO/UI no revelan prefijos | Build 0/0; suite 867 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Límite HTTP Google Calendar | OAuth y Calendar sin redirects, timeouts explícitos, JSON limitado a 1 MiB, errores a 4 KiB y sin reflejar cuerpos OAuth | Build 0/0; suite 869 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-14 | Mapping persistente Google Calendar | Eliminado lookup placeholder; mapping card-event por conexión permite create/update/delete reales y usa reloj inyectado | Build 0/0; suite 870 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-15 | Comando canónico de card mirror | Eliminado handler duplicado pointer-only; REST/MCP comparten creación real de Card + CardMirror y regla arquitectónica evita regresión | Suite 871 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-15 | Eliminación database log sink ficticio | Retirados sink/options/config que descartaban eventos; documentación normativa alinea console/file/OTLP reales | Build 0/0; suite 872 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-15 | Eliminación email de invitación simulado | Retirados puerto/adaptador que no enviaban correo y filtraban URL con token a logs; UI conserva entrega manual real | Build 0/0; suite 873 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-15 | Eliminación transporte email genérico sin uso | Retirados puerto, envelope, adapter log-only y registro DI sin consumidores; documentación ya no afirma SMTP inexistente | Build 0/0; suite 874 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-21 | Búsqueda relacional persistente | Índice singleton volátil reemplazado por lectura EF scoped; comandos sin mutaciones de índice; reinicios, tombstones y aislamiento cubiertos | Build 0/0; suite 878 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-21 | Puerto AI mínimo | Eliminados chat, embeddings y wire DTOs sin consumidores; el puerto conserva solo completion usada por el producto | Build 0/0; suite 879 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-21 | Eliminación proveedor AI simulado | Retirado fallback rule-based; único backend real OpenAI-compatible, defaults Ollama, configuración fail-fast y redirects bloqueados | Build 0/0; suite 885 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-21 | Límite HTTP del proveedor AI | Respuestas headers-first y acotadas a 1 MiB; JSON inválido estable; cuerpos externos fuera de logs y errores | Build 0/0; suite 889 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Limpieza del límite UI de errores | Eliminado componente placeholder; recuperación de banner sin `eval`, mediante helper explícito compatible con CSP | Build 0/0; suite 889 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Eliminación de bypasses de privilegios en Development | Retiradas rutas/commands para auto-admin y baja TOTP; bootstrap administrativo confinado al proceso de tests | Build 0/0; suite 892 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Callback OAuth Google Calendar acotado | Estado anónimo necesario conservado; JSON externo limitado a 1 MiB y errores 502 estables para payload inválido | Build 0/0; suite 895 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Límite real del broadcast interno | Autenticación previa, lectura manual máxima 64 KiB, soporte chunked y errores JSON 400 antes del dispatch SignalR | Build 0/0; suite 900 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Resolución relacional del broadcast | Eliminados escaneos cliente; lista y tarjeta se resuelven mediante consultas EF Core acotadas, sin tracking y traducibles por IDs tipados | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Consultas EF del núcleo Kanban | Board/List/Card filtran, unen, ordenan y proyectan en servidor; unstar usa delete set-based; excepción `DateTimeOffset` queda limitada a SQLite | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Consultas EF de capacidades Card | Aging/snooze/recurrence/mirrors/voting/checklists/comments/attachments usan filtros y agregados SQL; eliminado N+1 mirror-list | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Consultas EF de integraciones | GitHub/Slack/inbound-email/webhooks filtran relaciones, estado y páginas en SQL; sólo tokens CSV exactos y orden DateTimeOffset/SQLite quedan locales | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Consultas EF de identidad y acceso | Usuarios, preferencias, workspaces, invitaciones, API/OAuth/SCIM tokens y resets filtran por claves tipadas e índices en SQL; purgas no SQLite son set-based y SQLite sólo evalúa localmente `DateTimeOffset` antes de un delete set-based | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Consultas EF de actividad, automatizaciones y custom fields | Actividad filtra por board/card antes de paginar; reglas filtran estado y orden mediante EF Core; valores custom reemplazan dos escaneos globales por una consulta correlacionada EF | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Cierre de escaneos de persistencia | Jobs, extensiones, labels, notificaciones y exportación personal filtran/paginan mediante LINQ EF Core; mark-all-read actualiza todas las filas elegibles con `ExecuteUpdateAsync`; 0 SQL manual | Build 0/0; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Índices alineados a consultas EF | 19 índices compuestos/nuevos cubren filtros, orden y paginación reales; 13 índices prefijo redundantes fueron retirados mediante migración EF Core | Build 0/0; migración completa aplicada; suite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Corrección del objetivo multi-provider | Revertida la reducción SQLite-only; SQLite sigue siendo el entorno ordinario, mientras PostgreSQL y MariaDB/MySQL son compatibilidad obligatoria y gate automático de toda release final | Build 0/0; suite SQLite 901 pass, 0 fail, 1 skip | Incluido en este commit |
| 2026-08-22 | Modelo relacional neutral | Retiradas 19 declaraciones `HasColumnType("TEXT")` del modelo común; EF Core vuelve a seleccionar tipos nativos por provider y el historial SQLite preproducción fue normalizado sin migración destructiva | Build 0/0; modelo SQLite sin cambios pendientes; suite 901 pass, 0 fail, 1 skip; 0 SQL manual | Incluido en este commit |
| 2026-08-22 | Migraciones nativas PostgreSQL/MySQL | Assemblies separados generados por EF Core, factory design-time respeta la conexión externa, textos grandes usan tipos nativos y CI aplica historias limpias sobre PostgreSQL 17/MySQL 8.4; MariaDB no se anuncia tras reproducir incompatibilidad del provider Oracle | Build 0/0; suite 901 pass, 0 fail, 1 skip; SQLite sin cambios pendientes; PostgreSQL 17/MySQL 8.4 aplicados desde cero; snapshots sin cambios; actionlint 0; 0 SQL manual | Incluido en este commit |
| 2026-08-22 | Concurrencia optimista EF Core | Convención central cubre toda propiedad `RowVersion`, incluidas owned types; retiradas 43 configuraciones repetidas; entidades selladas en dominio y fallback del interceptor avanzan exactamente una versión; migraciones EF corrigen cinco defaults omitidos por provider | Build 0/0; suite 903 pass, 0 fail, 1 skip; regresiones 2/2; SQLite/PostgreSQL 17/MySQL 8.4 aplicados desde cero; snapshots sin cambios; 0 SQL manual | Incluido en este commit |
| 2026-08-22 | Outbox transaccional de eventos | Cada evento se persiste junto al agregado como entrega independiente por broadcaster; dispatch inmediato tras commit, recuperación hosted, leases multiworker y backoff reemplazan el fan-out best-effort que perdía fallos; serialización valida IDs/value objects y timestamps consultables usan ticks UTC portables; el wipe del Seeder incluye el outbox y reemplaza nombres de tabla/SQL dinámico por `ExecuteDeleteAsync` tipado | Build 0/0; suite 908 pass, 0 fail, 1 skip; regresiones outbox/modelo 7/7; SQLite/PostgreSQL 17/MySQL 8.4 desde cero; snapshots sin cambios; 0 SQL manual en `src` | Incluido en este commit |
| 2026-08-22 | Cierre de abstracciones de Application | Reubicados cinco puertos públicos de calendario, TOTP pendiente y realtime bajo `Application.Abstractions`; eliminadas ubicaciones y archivo API obsoletos sin aliases; una regla global inspecciona toda interfaz pública del assembly y conserva la convención de nombres | Build 0/0; suite 908 pass, 0 fail, 1 skip; invariants nuevos 2/2; 0 referencias legacy; 0 SQL manual en `src` | Incluido en este commit |
| 2026-08-22 | División del slice de comandos Card | Eliminado `CardCommands.cs` de 1.114 líneas; 16 comandos/handlers y mapping se reparten entre mutaciones, planificación, ciclo de vida, relaciones y mapeo, conservando tipos y namespace canónicos sin compatibilidad adicional | Build 0/0; suite 908 pass, 0 fail, 1 skip; máximo 344 líneas por archivo; 0 SQL manual en `src` | Incluido en este commit |
| 2026-08-22 | División del slice Checklists | Eliminado `ChecklistCommands.cs` de 647 líneas; DTOs/query, ciclo de vida del checklist, edición de ítems y estado/eliminación quedan en cuatro archivos cohesionados, sin alterar tipos públicos ni namespaces | Build 0/0; suite 908 pass, 0 fail, 1 skip; máximo 223 líneas por archivo; inventario público idéntico; 0 SQL manual en `src` | Incluido en este commit |

## 5. Criterio de completitud

El plan estará completo cuando todas las fases estén verificadas o cada excepción restante tenga una decisión explícita, evidencia y responsable. “Compila” no es suficiente: la arquitectura declarada, el código, las pruebas, la UI Radzen y la documentación deben describir el mismo sistema.
