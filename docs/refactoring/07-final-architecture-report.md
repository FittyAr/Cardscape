# Informe final de arquitectura y modernización

Fecha de cierre: 2026-09-14

## Dictamen

La modernización integral definida en el plan activo está ejecutada. Cardscape
queda organizado por capacidades sobre .NET 10, con límites Clean Architecture
comprobables, persistencia EF Core como vía única, contratos HTTP/OpenAPI
explícitos, UI Blazor basada en Radzen y observabilidad estructurada mediante
`LoggerMessage` y OpenTelemetry.

El cierre del plan no significa deuda cero. Significa que cada hallazgo quedó
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
hotspot MIME principal y el dispatch de broadcast ya fueron reducidos; los
siguientes bloques deben atender, en orden, SCIM Groups y políticas de presentación Activity.
Responsable: mantenedores de Application/Infrastructure. Gate: cobertura y
regresiones focalizadas en cada cambio.

### 2. MariaDB es requisito, no compatibilidad actual

Las releases finales deben ser compatibles con PostgreSQL, MySQL y MariaDB.
SQLite sigue siendo el motor ordinario de desarrollo y pruebas. PostgreSQL 17 y
MySQL 8.4 tienen providers EF Core 10 estables, migraciones nativas y matriz CI.

MariaDB 11.4 continúa bloqueado porque `MySql.EntityFrameworkCore` falla antes de
aplicar el esquema y Pomelo todavía no publica una versión estable para EF Core
10. Por tanto, no puede salir una release final que prometa MariaDB hasta que el
gate de `docs/operations/12-mariadb-future-work.md` pase sobre un servicio real.
Esto preserva la instrucción de compatibilidad y evita el apaño de equiparar
protocolo wire con soporte EF Core.

Responsable: mantenedores de persistencia/release. Decisión siguiente: adoptar el
primer provider EF Core 10 estable que supere generación, migración limpia,
integración y Compose sobre MariaDB LTS; no usar previews ni SQL manual.

### 3. Dependencia SAML legacy

Sustainsys 2.11.0 arrastra dos paquetes IdentityModel deprecados, sin advisory
conocido. ADR 0012 limita exactamente esa excepción; CI falla ante cualquier
otra deprecación y ante vulnerabilidades. Responsable: mantenedores de seguridad.
Decisión siguiente: migrar cuando exista una línea mantenida para .NET 10 o
reemplazar el adaptador SAML completo, sin fijar transitivos antiguos.

### 4. Smoke local de imagen

El daemon Docker no fue accesible desde este host. Compose fue validado y CI
construye, inicia y exige readiness de la imagen, por lo que el gate no se omite.
Responsable: pipeline de release. Decisión siguiente: conservar el smoke como
condición obligatoria de release y repetir localmente cuando el daemon esté
disponible.

## Verificación de cierre

- Build Release del hook: 0 warnings, 0 errores.
- Suite integral: 1.011/1.011 pruebas, 0 fallos, 0 omitidos.
- Arquitectura: 55/55; E2E: 7/7; Funcional: 1/1; Integración: 266/266;
  SDK: 19/19; Seguridad: 23/23; Unitarias: 640/640.
- Checkboxes abiertos en el plan activo: 0.
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
