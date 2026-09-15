# Auditoría de calidad y cobertura de pruebas

Fecha: 2026-09-14

## Alcance y método

Se instrumentaron con Coverlet los siete proyectos de pruebas. El cálculo
deduplica líneas y métodos cubiertos por más de un proyecto y excluye código
generado, `obj`, migraciones EF Core, snapshots y archivos `Designer`.

La extensión .NET anunciada por `test-analysis-extensions` y los scripts
anunciados por `coverage-analysis` no estaban incluidos en la instalación
local. Se aplicó el fallback documentado: detección xUnit/FluentAssertions y
cálculo directo desde Cobertura mediante
`CRAP = complexity² × (1 - coverage)³ + complexity`.

## Resultado

- 1.028 pruebas superadas, 0 fallidas y 0 omitidas.
- 838 métodos `[Fact]`/`[Theory]` y 2.359 llamadas de assertion/verificación.
- 0 pruebas sin assertions: dos candidatos heurísticos delegaban en
  `AssertProblemAsync` y `VerifyNoOtherCalls`.
- 0 assertions siempre verdaderas y 0 assertions async sin `await` detectadas.
- 47,27% de cobertura de líneas y 32,93% de ramas sobre código mantenible.
- 3.407 métodos analizados y 185 con CRAP superior a 30.

Los delays encontrados pertenecen a polling E2E/integración acotado y
cancelable. No se usan sleeps como sustituto de una assertion. Se retiraron dos
`Console.WriteLine` diagnósticos residuales del test de guard de región.

## Riesgo priorizado

1. `AttachmentUploadPolicy.IsBlockedMimeType`: CRAP 9794,18; complejidad 128;
   cobertura 16,13%. Debe convertirse en política basada en datos con theories.
2. `BoardBroadcastEndpoints.DispatchAsync`: CRAP 8298,07; complejidad 114;
   cobertura 14,29%. Requiere escenarios por tipo de evento, ownership y error.
3. `ScimService.PatchGroupAsync`: CRAP 1980; complejidad 44; cobertura 0%.
   Requiere matrices add/remove/replace, miembros inexistentes y concurrencia.
4. `Activity.KindLabel`/`KindBadgeStyle`: CRAP 1806/1482 y cobertura 0%.
   Conviene extraer la política del Razor y probar todos los valores.

## Remediación ejecutada

- `AttachmentUploadPolicy.IsBlockedMimeType` dejó de ser un switch de 27 ramas
  y usa un `FrozenSet<string>` ordinal e inmutable.
- Una theory valida los 27 MIME bloqueados después de normalizar casing/espacios,
  el código de error estable y la ausencia de I/O/persistencia. Junto con los
  escenarios de éxito y compensación, el grupo ejecuta 29/29 tests.
- `BoardBroadcastEndpoints.DispatchAsync` dejó de contener 20 ramas duplicadas:
  usa un registro `FrozenDictionary` de handlers genéricos tipados y claves
  `nameof(IBoardClient.*)`. Una theory ejercita las 20 combinaciones método/payload
  por HTTP; la clase completa ejecuta 30/30 tests conservando sus negativos.

El informe reproducible de esta ejecución está en
`TestResults/coverage-analysis/coverage-analysis.md`; `TestResults` es un
artefacto local ignorado y no forma parte del historial Git.
