# SDK release process

> The `sdk/Cardscape.Sdk` package is the hand-written C#
> client for the Cardscape REST API. The package is
> **multi-target** (`netstandard2.0` + `net8.0` + `net10.0`)
> and ships as a `.nupkg` + `.snupkg` pair. This document is
> the recipe the maintainer follows to ship a release; it is
> the same recipe the CI uses, in the same order.

---

## 1. Versioning

The SDK follows the API server's semantic version. The
`<Version>` element in `sdk/Cardscape.Sdk/Cardscape.Sdk.csproj`
is bumped in the same commit that bumps the API's
`<Version>` in `Directory.Build.props`. Both files use the
**same** string (e.g. `1.2.0`).

The pre-release suffix follows NuGet's standard (`-alpha.1`,
`-rc.1`, …). Stable releases drop the suffix entirely.

## 2. Pre-release checklist

1. `Directory.Packages.props` — every PackageReference the
   SDK depends on is pinned to a known-good version.
2. `sdk/Cardscape.Sdk/Cardscape.Sdk.csproj` — `<Version>` is
   bumped and the multi-target line-up is correct.
3. `sdk/Cardscape.Sdk/Models.cs` + `SubClients.cs` — every
   DTO and method matches the API surface the SDK is supposed
   to cover. The round-trip test project
   `tests/Cardscape.Sdk.Tests` exercises both.
4. `tests/Cardscape.Sdk.Tests` — all 11 tests are green on
   every target (`net8.0` + `net10.0`). Run:
   ```pwsh
   dotnet test sdk/Cardscape.Sdk.slnx -c Release
   ```
5. `git status` is clean. The bump commit + the source
   change are on `master` and pushed.

## 3. Pack the artefacts

```pwsh
# From the repo root.
dotnet pack sdk/Cardscape.Sdk/Cardscape.Sdk.csproj -c Release
```

The command produces:

- `sdk/Cardscape.Sdk/bin/Release/Cardscape.Sdk.<version>.nupkg`
- `sdk/Cardscape.Sdk/bin/Release/Cardscape.Sdk.<version>.snupkg`

The `snupkg` is the symbol package (Source Link + embedded
sources). Consumers debugging into the SDK from their app
land in the matching commit.

## 4. Inspect the package

```pwsh
# List the contents of the .nupkg (it's a zip).
Expand-Archive `
  -Path sdk/Cardscape.Sdk/bin/Release/Cardscape.Sdk.<version>.nupkg `
  -DestinationPath /tmp/sdk-pkg -Force
Get-ChildItem /tmp/sdk-pkg
# Expect: lib/netstandard2.0/, lib/net8.0/, lib/net10.0/,
#         Cardscape.Sdk.nuspec, README.md, [Content_Types].xml
```

`lib/net*` should contain `Cardscape.Sdk.dll` for every
target. The nuspec's `<dependencies>` should NOT pin
`System.Text.Json` for the net8.0/net10.0 targets (it lives
in the BCL on those targets).

## 5. Publish to NuGet

```pwsh
# Push the package + the symbol package.
dotnet nuget push `
  sdk/Cardscape.Sdk/bin/Release/Cardscape.Sdk.<version>.nupkg `
  --api-key $env:NUGET_API_KEY `
  --source https://api.nuget.org/v3/index.json
dotnet nuget push `
  sdk/Cardscape.Sdk/bin/Release/Cardscape.Sdk.<version>.snupkg `
  --api-key $env:NUGET_API_KEY `
  --source https://api.nuget.org/v3/index.json
```

The push is **idempotent**: NuGet rejects a re-push of the
same `<version>`. Bump `<Version>` to publish a fix.

## 6. Smoke-test the published package

After the push, the package is on `nuget.org` within ~30 s.
Spin up a sandbox project and add the package:

```pwsh
mkdir /tmp/sdk-smoke && cd /tmp/sdk-smoke
dotnet new console
dotnet add package Cardscape.Sdk --version <version>
```

A 5-line `Program.cs` that hits the `/health` endpoint of a
local API instance confirms the package is consumable. The
test project `tests/Cardscape.Sdk.Tests` does the same
against a stub `HttpMessageHandler` so the maintainer does
not need a live API.

## 7. Tag the release

```pwsh
git tag v<version>
git push origin v<version>
```

The tag triggers the CI release job (defined in
`.github/workflows/ci.yml`) which runs the full test suite +
the SDK pack step. The release job is **idempotent** with
respect to the tag: a re-push of an existing tag does not
re-publish the NuGet package.

## 8. Out of scope (deferred to v1.3+)

- **Multi-package split** — boards / cards / labels each
  published as a separate NuGet package. Currently the
  whole SDK is one package. The split becomes worthwhile
  when the SDK grows past ~50 types.
- **CI-driven publish** — the CI does NOT call
  `dotnet nuget push`; the maintainer does the push by
  hand from a local checkout with the API key. The
  pipeline that does the push is a follow-up so a
  compromised CI cannot publish malicious packages.
- **API surface auto-gen** — the SDK is hand-written. An
  NSwag / Kiota generator that pulls from the OpenAPI
  spec would shrink the maintenance burden but is a
  separate workstream.
