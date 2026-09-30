# Informe final de arquitectura y modernización

Fecha de cierre de fases: 2026-09-14; cierre del gate multi-provider: 2026-09-30.

## Dictamen

La modernización integral definida en el plan activo está ejecutada. Cardscape
queda organizado por capacidades sobre .NET 10, con límites Clean Architecture
comprobables, persistencia EF Core como vía única, contratos HTTP/OpenAPI
explícitos, UI Blazor basada en Radzen y observabilidad estructurada mediante
`LoggerMessage` y OpenTelemetry.

El cierre del checklist no significa deuda cero ni pruebas exhaustivas de cada
funcionalidad en cada motor. El gate MariaDB se verificó localmente el 2026-09-30
con un provider estable, servicios reales e imagen Production. Cada hallazgo quedó
corregido, protegido por evidencia automatizada o registrado como riesgo con un
gate y un responsable claro. No se conservó compatibilidad legacy del producto.

## Estado por área

| Área | Estado resultante | Evidencia principal |
|---|---|---|
| Capas y dependencias | Domain es independiente; Application sólo depende de Domain; adaptadores y hosts permanecen fuera | Grafo exacto y 55 architecture tests |
| Abstracciones | Puertos públicos pertenecen a Application; interfaces ceremoniales y service locators fueron eliminados | Gates de ownership/naming y composición DI |
| Dominio y casos de uso | Slices por capacidad, handlers sellados, invariantes y cancelación explícita | Build con warnings como errores y gates async |
| Persistencia | LINQ/EF Core para consultas y mutaciones; sin SQL manual en `src`; outbox/inbox transaccionales | Suite SQLite y migraciones nativas por provider |
| API | 212 operaciones con semántica HTTP, Problem Details y OpenAPI explícitos; aliases legacy retirados | Tests de integración/OpenAPI y gates de endpoints |
| Seguridad | Límites tenant, secretos protegidos, OAuth/OIDC/SAML/SCIM/2FA y SSRF revisados | Suites Security/Integration y ADR de excepción SAML |
| MCP | Identidad única, scopes fail-closed y broadcast entre procesos observable | Tests E2E por protocolo y gates del catálogo |
| Web | Blazor WASM 10 con componentes Radzen, estados de error explícitos, localización y accesibilidad | Build Web, tests de recursos y gates Razor |
| Operación | Health real, OpenTelemetry, contenedor endurecido, CI y supply chain fail-closed | Compose validado y workflow de release |
| Pruebas | Sin skips ni assertions vacías; suites funcional/E2E orientadas a fronteras observables | Auditorías 04–06 y pipeline `.testagent` |

## Deuda residual justificada

### 1. Cobertura y riesgo de cambio

La medición depurada es 47,27% de líneas y 32,93% de ramas, con 185 de 3.407
métodos por encima de CRAP 30. No se presenta como cobertura suficiente. El
hotspot MIME principal y el dispatch de broadcast ya fueron reducidos. El
2026-09-30 se corrigió además la eliminación filtrada de miembros SCIM, con
regresiones que fallaron antes del cambio, y se extrajo/probó la presentación
Activity para los 25 tipos reales. La matriz SCIM Groups ahora cubre además
add/replace en ambos órdenes, valores tipados/JSON y reemplazo vacío. Se corrigió
además el alta indebida por paths con prefijo `members` (SCIM 25/25;
unitarias completas 712/712, sin fallos ni omitidos, 2026-09-30).
También se corrigieron operaciones sin path y payloads de miembros inválidos:
normalización por atributos y validación previa evitan eliminar membresías
omitidas o aplicar parcialmente un request con valores malformados (SCIM 40/40,
unitarias 727/727). El status del cuerpo de errores SCIM ahora coincide con HTTP,
y valores inválidos publican `invalidValue`: integración SCIM 8/8 y arquitectura
55/55. También se rechaza el borrado explícito del propietario antes de mutar,
con error de mutabilidad verificable por HTTP y GET posterior (SCIM unit 42/42,
unitarias 729/729, SCIM HTTP 9/9). Operaciones desconocidas, paths no admitidos,
remove sin destino y requests vacíos ahora se rechazan explícitamente sin cambios
parciales (unitarias 739/739, SCIM HTTP 12/12). La superficie Groups implementada
queda delimitada; no se promete soporte de filtros arbitrarios o subatributos.
La matriz completa de errores del recurso Users sigue fuera de este bloque;
estas correcciones puntuales no constituyen una certificación del protocolo completo.
La paginación Groups también respeta count cero/negativo e índices fuera de
página, devuelve metadatos exactos y evita el lookup de miembros si no entrega
recursos (SCIM unit 60/60, unitarias 747/747, SCIM HTTP 16/16).
PATCH active de Users ya normaliza bool CLR/JSON y objetos sin path; antes
ignoraba los cambios provenientes de HTTP. Los requests inválidos fallan antes
de mutar y las operaciones válidas conservan el orden y un único save.
SCIM unit 83/83 y SCIM HTTP 18/18 verifican esos contratos, sin certificar
el resto de atributos ni la totalidad del protocolo.
Responsable: mantenedores de Application/Infrastructure. Gate: cobertura y
regresiones focalizadas en cada cambio.

### 2. Compatibilidad multi-provider y mantenimiento del gate

Las releases finales deben ser compatibles con PostgreSQL, MySQL y MariaDB.
SQLite sigue siendo el motor ordinario de desarrollo y pruebas. PostgreSQL 17 y
MySQL 8.4 y MariaDB 11.4 tienen providers EF Core 10 estables, migraciones nativas
y matriz CI. MariaDB usa Microting 10.0.12 mediante ADR 0013, no Oracle ni un alias.

Los cuatro motores pasaron persistencia/concurrencia/modelo alineado. La política
EF Core de cambios pendientes es `Throw` explícito en runtime/design-time;
se retiró la supresión global y una regresión falló antes de corregirla.
API y MCP incluyen todas las historias externas; el publish de MCP se comprobó.
CI exige historias externas, pruebas por motor y smoke Production SQLite/MariaDB
antes de release. Es configuración validada localmente con actionlint, no una
ejecución GitHub Actions que todavía no se haya observado.

Responsable: mantenedores de persistencia/release. Siguiente decisión: mantener
el gate verde con cada cambio de modelo/provider, revisar mantenimiento y
advisories del fork y ampliar contratos funcionales por motor según riesgo.
Esta suite acotada no certifica todas las consultas ni todas las funcionalidades.

### 3. Dependencia SAML legacy

Sustainsys 2.11.0 arrastra dos paquetes IdentityModel deprecados, sin advisory
conocido. ADR 0012 limita exactamente esa excepción; CI falla ante cualquier
otra deprecación y ante vulnerabilidades. Responsable: mantenedores de seguridad.
Decisión siguiente: migrar cuando exista una línea mantenida para .NET 10 o
reemplazar el adaptador SAML completo, sin fijar transitivos antiguos.

### 4. Smoke local de imagen verificado

Con Docker disponible se construyó e inició la imagen real en Production para
SQLite y MariaDB. Ambos contenedores alcanzaron readiness 200 y pasaron
registro y escritura/lectura autenticada de un workspace Unicode. Se corrigieron
el contexto Docker sin `.editorconfig`, el `ENV` SQLite truncado por un espacio
y la configuración CORS ausente del smoke CI. El stack aislado no modificó Certaro.
Responsable: pipeline de release. Conservar este smoke obligatorio y usar secretos
y volúmenes durables en despliegues reales, nunca las credenciales del fixture.

## Verificación de cierre

Actualización 2026-09-30: rebuild completo Release no incremental, 0 warnings
y 0 errores. Suite integral 1.188/1.188, sin fallos ni omitidos: arquitectura
55, E2E 7, funcional 1, integración 313, SDK 19, seguridad 23 y unitarias 770.
SCIM unit 83/83 y HTTP 18/18. No se recalculó cobertura/CRAP; sus cifras
anteriores conservan su fecha original. Gate de providers 12/12 (SQLite incluido),
Production-image smoke SQLite/MariaDB verde y NuGet sin vulnerabilidades conocidas.
Formato y actionlint limpios. La publicación autorizada abrió la verificación
remota del §8 del plan: se corrigieron configuración del runtime, redirección
Bash, restores independientes y lectura de referencias MSBuild en Linux.
Su cierre requiere observar CI verde; los resultados locales no lo sustituyen.
La cobertura y excepción SAML siguen como deuda residual explícita.

Evidencia histórica del cierre 2026-09-14:

- Build Release del hook: 0 warnings, 0 errores.
- Suite integral: 1.011/1.011 pruebas, 0 fallos, 0 omitidos.
- Arquitectura: 55/55; E2E: 7/7; Funcional: 1/1; Integración: 266/266;
  SDK: 19/19; Seguridad: 23/23; Unitarias: 640/640.
- Checkboxes abiertos al cierre histórico: 0. El plan actual explicita un
  gate obligatorio MariaDB pendiente; no equivale a una release certificada.
- SQL manual en fuentes de producto: 0.
- Llamadas legacy `ILogger.Log*` en fuentes de producto: 0.

## Decisiones de mantenimiento

1. No aceptar nuevas abstracciones sin dos consumidores o una frontera externa
   verificable.
2. Mantener EF Core/LINQ como política exclusiva; cualquier excepción requiere
   imposibilidad demostrada, ADR y matriz de todos los providers de release.
3. Mantener Radzen como única biblioteca de interacción Web; CSS aislado sólo
   para presentación sin equivalente del componente.
4. Incorporar cada regresión repetible al proyecto de arquitectura o a una prueba
   de comportamiento, evitando tests que invoquen clases concretas del host.
5. Ejecutar la matriz multi-provider y los gates de supply chain antes de toda
   release final.

## Documentos de evidencia

- `03-whole-project-modernization-plan.md`: checklist y registro cronológico.
- `04-test-quality-audit.md`: assertions, cobertura y CRAP.
- `05-functional-e2e-behavior-audit.md`: contratos black-box.
- `06-architecture-gates-audit.md`: mapa de restricciones ejecutables.
- `../architecture/02-multi-provider-persistence.md`: modelo multi-provider.
- `../operations/12-mariadb-future-work.md`: gate obligatorio MariaDB.
- `../adr/0012-saml-legacy-dependency.md`: excepción SAML acotada.
