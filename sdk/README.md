# Cardscape SDK

The hand-written C# client for the Cardscape REST API.

## Layout

```
sdk/
├── Cardscape.Sdk.slnx         # dedicated solution for the SDK package
└── Cardscape.Sdk/
    ├── Cardscape.Sdk.csproj   # multi-targets netstandard2.0 + net8.0
    ├── CardscapeClient.cs     # top-level typed client (HttpClient-based)
    ├── Models.cs               # DTOs
    ├── SubClients.cs           # per-resource sub-clients (boards, cards, …)
    ├── IsExternalInit.cs      # polyfill for C# 9 init-only setters
    └── README.md               # this file
```

The SDK is included in the root `Cardscape.slnx` under
a `/sdk/` folder. The dedicated `sdk/Cardscape.Sdk.slnx`
is the published package boundary: it builds the
`Cardscape.Sdk.1.1.0.nupkg` / `.snupkg` artifacts
without dragging the rest of the solution into the
package pipeline.

## Build

```bash
# From the repo root:
dotnet build sdk/Cardscape.Sdk.slnx -c Release
# or
dotnet pack sdk/Cardscape.Sdk/Cardscape.Sdk.csproj -c Release
```

The build emits the package artifacts at
`sdk/Cardscape.Sdk/bin/Release/`.

## Install

```bash
dotnet add package Cardscape.Sdk --version 1.1.0
```

## Usage

```csharp
using Cardscape.Sdk;

var client = new CardscapeClient(
    baseAddress: new Uri("https://api.cardscape.example/"),
    accessToken: "your-bearer-token");

// Per-resource sub-clients surface the high-level
// operations the SDK targets. The set is curated, not
// auto-generated from OpenAPI.
CardscapeBoard board = await client.Boards.GetAsync(boardId);
IReadOnlyList<CardscapeCardSummary> cards =
    await client.Cards.ListAsync(boardId, includeArchived: false);
```

See `sdk/Cardscape.Sdk/CardscapeClient.cs` and
`sdk/Cardscape.Sdk/SubClients.cs` for the full API.

## Versioning

The SDK follows the API server's semantic version. The
`Version` element in `Cardscape.Sdk.csproj` is bumped in
the same commit that bumps the API's `Version` in
`Directory.Build.props`.
