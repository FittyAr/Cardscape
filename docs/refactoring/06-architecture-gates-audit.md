# Cierre de validaciones de arquitectura

Fecha: 2026-09-14

## Cobertura automatizada

La suite de arquitectura convierte los defectos repetibles encontrados durante la
modernización en restricciones ejecutables:

- grafo exacto de referencias y dependencias permitidas por capa;
- ownership, ubicación y nombres de abstracciones, sin interfaces públicas en
  Infrastructure o Seeder;
- entidades y handlers sellados, cancelación async y Problem Details canónico;
- scopes, identidad y nombres públicos canónicos de MCP;
- Radzen-only, localización, accesibilidad, validación, paging, callbacks observables
  y preservación explícita de estados de fallo en Web;
- ausencia de proveedores simulados, bypasses de privilegios y sinks placeholder.

## Defecto adicional cerrado

La auditoría funcional/E2E eliminó una prueba que resolvía
`HttpMcpResourceNotifier` y ejecutaba directamente su método. El nuevo gate
`FunctionalAndE2ETests_DoNotResolveConcreteHostTypes` inspecciona ambos proyectos
black-box y rechaza `GetRequiredService<T>` cuando `T` pertenece a los namespaces
concretos de los hosts API o MCP.

La regla no impide preparación de fixtures mediante persistencia o servicios de
aplicación: esos accesos configuran el escenario, mientras que el comportamiento
bajo prueba continúa entrando y saliendo por HTTP/protocolo.

## Límite deliberado

Los lifetimes no se validan mediante coincidencias textuales generales: su
corrección depende del grafo de DI construido y de la semántica de cada servicio.
Los casos comprobables ya están cubiertos por pruebas de composición/arranque. Se
prefiere esa evidencia ejecutable frente a un gate estático propenso a falsos
positivos.

## Verificación

- Suite de arquitectura: 55/55, sin fallos ni omitidos.
- Compilación del proyecto modificado: 0 warnings, 0 errores.
- Revisión de pseudo-mutación: reintroducir la resolución concreta eliminada hace
  fallar `FunctionalAndE2ETests_DoNotResolveConcreteHostTypes`.
