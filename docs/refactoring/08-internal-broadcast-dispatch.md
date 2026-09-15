# Remediación del dispatch de broadcast interno

Fecha: 2026-09-14

## Hallazgo

`BoardBroadcastEndpoints.DispatchAsync` era el segundo hotspot del informe CRAP:
un switch de 20 ramas repetía deserialización, null-check, broadcast y retorno.
Sólo `CardCreated` tenía un escenario positivo, por lo que las otras 19 entradas
del protocolo podían desviarse sin romper la suite.

## Cambio

- Un `FrozenDictionary<string, BroadcastHandler>` reemplaza el switch.
- Las claves usan `nameof(IBoardClient.Method)` y comparación ordinal.
- `CreateHandler<TPayload>` conserva deserialización fuertemente tipada y delega
  en el método exacto de `IBoardClient` mediante lambdas estáticas.
- No se introdujo reflection, `dynamic`, service locator ni dispatch ambiguo.
- La resolución board/list/card continúa mediante consultas LINQ/EF Core acotadas.

## Verificación

- `Broadcast_EachSupportedMethod_WithMatchingPayload_Returns202` contiene una
  fila por cada una de las 20 operaciones y verifica HTTP 202.
- La clase `BoardBroadcastEndpointTests` completa pasa 30/30: configuración,
  autenticación, límite anunciado/chunked, JSON inválido, payload incompatible,
  método desconocido y resolución por list/card permanecen cubiertos.
- API e Integration compilan con 0 warnings y 0 errores.

## Revisión de mutaciones

Eliminar/renombrar una clave, cambiar su tipo de payload o asociarla a un método
incompatible hace fallar la fila exacta de la teoría exhaustiva. Los negativos
detectan relajación del secreto, del cap de 64 KiB o de los errores canónicos.
