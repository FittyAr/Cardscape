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
- Diseñar persistencia para SQLite, PostgreSQL y MariaDB; validar automáticamente en SQLite hasta ampliar la matriz.
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
- [ ] Existen archivos con demasiadas responsabilidades: `CardDetail.razor` (~58 KB), `BoardDetail.razor` (~34 KB), `CardCommands.cs` (~40 KB), el registro DI de Infrastructure (~32 KB), `BoardsTools.cs` (~31 KB) y `Api/Program.cs` (~18 KB).
- [ ] La migración documentada por proveedor contradice la estructura actual de migraciones consolidadas; debe decidirse y comprobarse una única estrategia multi-provider.
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
- [ ] Revisar ubicación y dependencia de cada abstracción; Domain no debe depender de frameworks y Application sólo de Domain/abstracciones necesarias.
- [x] Auditar composición DI de API, MCP y Seeder: lifetimes, duplicación, validación al arranque y opciones tipadas.
- [ ] Revisar boundaries y vertical slices; dividir archivos monolíticos por caso de uso sin crear capas adicionales.
- [ ] Revisar el rol del SDK público y evitar duplicación de contratos con Web/API.
- [ ] Alinear solución, Docker, CI, scripts y documentación con el mismo conjunto de proyectos.
- [x] Reconciliar documentación normativa con Wolverine, .NET 10 y versiones instaladas.

### Fase 2 — Superficies críticas

- [ ] Autenticación/autorización: JWT, API tokens, OAuth/OIDC, SAML, SCIM, 2FA, políticas y aislamiento multi-tenant.
- [ ] Persistencia: modelo EF, transacciones, concurrencia, índices, consultas N+1, tracking y compatibilidad de los tres providers.
- [ ] Gestión de secretos, cifrado, datos personales, borrado/anominización y retención.
- [ ] Webhooks, importaciones, adjuntos y clientes HTTP: SSRF, validación, límites, reintentos, timeouts e idempotencia.
- [ ] Wolverine/background jobs: scopes, retries, outbox/inbox, cancelación y consistencia de eventos. Claim multi-worker ya es atómico y está probado; quedan outbox/inbox, cancelación y consistencia de eventos.
- [ ] MCP: autorización equivalente a REST, lifetimes, transporte, suscripciones e idempotencia. Scopes, lifetime/composición, aislamiento de suscripciones e idempotencia global ya corregidos; quedan transporte y coordinación de primeras ejecuciones concurrentes.
- [ ] Observabilidad: logs estructurados, correlación, trazas, métricas, health checks y ausencia de PII/secrets.

### Fase 3 — API y contratos

- [ ] Revisar semántica HTTP, Problem Details, validación, cancelación y códigos de estado de todos los endpoints.
- [ ] Eliminar endpoints legacy y contratos duplicados porque no se exige retrocompatibilidad.
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
| 2026-08-11 | Claim atómico de background jobs | Cada candidato usa UPDATE guardado por status + RowVersion; workers concurrentes reciben batches disjuntos | Build 0/0; suite 799 pass, 0 fail, 1 skip | Pendiente |

## 5. Criterio de completitud

El plan estará completo cuando todas las fases estén verificadas o cada excepción restante tenga una decisión explícita, evidencia y responsable. “Compila” no es suficiente: la arquitectura declarada, el código, las pruebas, la UI Radzen y la documentación deben describir el mismo sistema.
