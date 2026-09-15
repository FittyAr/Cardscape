# Auditoría de comportamiento de pruebas funcionales y E2E

Fecha: 2026-09-14

## Alcance y criterio

Se revisaron las ocho pruebas declaradas en `Cardscape.FunctionalTests` y
`Cardscape.E2ETests`. Una prueba se considera orientada a comportamiento cuando
actúa sobre una frontera observable del sistema (HTTP, protocolo MCP, health o
topología desplegada) y verifica su resultado. El uso de servicios o persistencia
en la preparación de un escenario no convierte por sí solo la aserción en un
detalle de implementación.

## Resultado

- `GoldenPath_RegisterCreateWorkspaceBoardListCard_MoveAndArchive_AllSucceed`
  recorre exclusivamente la API pública y valida el estado resultante.
- Los contratos de salud, autenticación MCP y del endpoint interno entre procesos
  se ejercitan por HTTP. La preparación directa del usuario y su token evita
  convertir esta suite en una repetición del flujo funcional de registro.
- La comprobación de puertos es un contrato de topología de la fixture de dos
  procesos: demuestra direcciones distintas y el destino configurado entre hosts.
- `Api_Mutation_Reaches_Mcp_Broadcaster_Across_Processes` crea todos sus recursos
  mediante la API, ejecuta una mutación pública y observa la entrega en MCP.
- Se eliminó `Api_Notifier_Can_Call_Mcp_Directly_Across_Processes`: resolvía
  `HttpMcpResourceNotifier` desde el contenedor y llamaba directamente a su método,
  por lo que verificaba una clase concreta y duplicaba el escenario público.
- También se retiró el helper diagnóstico `DumpMcpSubscriptionsAsync`, sin usos.

## Regla de mantenimiento

Las suites funcional y E2E deben expresar contratos en fronteras desplegables.
Los accesos internos quedan limitados al `Arrange` imprescindible de una fixture;
ninguna aserción debe depender de una clase concreta, una llamada interna, el
orden de invocaciones o la forma privada del grafo DI.

## Verificación

- `Cardscape.FunctionalTests`: 1 prueba.
- `Cardscape.E2ETests`: 7 pruebas.
- Revisión de pseudo-mutaciones: omitir la mutación pública, el header de
  autenticación, el URI de tablero o el estado archivado hace fallar el escenario
  que protege el contrato correspondiente.
