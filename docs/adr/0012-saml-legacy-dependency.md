# ADR 0012 — Excepción temporal para la dependencia SAML legacy

- Estado: aceptado
- Fecha: 2026-09-11
- Responsable de revisión: mantenedores de Cardscape

## Contexto

Cardscape implementa SAML mediante `Sustainsys.Saml2.AspNetCore2` 2.11.0,
la última versión publicada por ese proveedor. Su grafo transitivo incluye
`Microsoft.IdentityModel.Tokens.Saml` 5.2.4 y `Microsoft.IdentityModel.Xml`
5.2.4, paquetes marcados como legacy por NuGet. La auditoría no reporta
vulnerabilidades conocidas, pero sí deprecación.

No existe una versión más reciente del paquete ASP.NET Core de Sustainsys a
la cual actualizar sin sustituir la implementación SAML completa. Eliminar
SAML tampoco es aceptable mientras siga siendo una superficie requerida del
producto.

## Decisión

Se permite exclusivamente esa pareja de dependencias transitivas legacy.
CI enumera todo el grafo restaurado y falla ante cualquier otro paquete
deprecado. `NuGetAuditMode=all` y `NU1901`–`NU1904` como errores bloquean
además cualquier vulnerabilidad conocida, incluida una que aparezca en esta
excepción.

No se fijan versiones transitivas antiguas ni se ocultan warnings para
mantener Sustainsys funcionando.

## Revisión

La excepción debe retirarse cuando Sustainsys publique una línea moderna
compatible con .NET 10 o cuando Cardscape reemplace el adaptador SAML por una
implementación mantenida. La revisión de dependencias de cada release es
responsable de comprobarlo mediante `dotnet package list --deprecated`.
