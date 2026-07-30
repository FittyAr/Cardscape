# Cardscape.Sdk

Typed C# client for the [Cardscape](https://github.com/cardscape/cardscape)
REST API. Targets `netstandard2.0` and `net8.0` for the broadest
reach. Built and published automatically by
`dotnet pack` (see `GeneratePackageOnBuild=true` in
`Cardscape.Sdk.csproj`).

The SDK covers the **30 most-used endpoints** with strongly-typed
methods and DTOs. The rest of the Cardscape surface stays reachable
through the lower-level `ICardscapeClient.SendAsync(...)` /
`SendAsync<T>(...)` methods on the public client.

## Install

```bash
dotnet add package Cardscape.Sdk --version 1.1.0
```

## Quickstart

```csharp
using Cardscape.Sdk;

CardscapeClient client = new(new CardscapeClientOptions
{
    BaseAddress = new Uri("https://cardscape.example.com/"),
    AccessToken = () => Task.FromResult("eyJhbGciOi...")
});

// List the workspaces the current user is a member of.
IReadOnlyList<WorkspaceDto> workspaces = await client.Workspaces.ListAsync();

// Create a new board.
BoardDto board = await client.Boards.CreateAsync(new CreateBoardRequest(
    workspaceId: workspaces[0].Id,
    name: "My New Board",
    description: "Spike for the migration",
    visibility: BoardVisibility.Private));
```

## Coverage

The hand-written methods today cover:

- `client.Workspaces`: list, get, create, set region, list members
- `client.Boards`: list (workspace / starred), get, create, rename,
  set description, set visibility, archive, unarchive, star,
  unstar, export, iCalendar
- `client.Lists`: list, create
- `client.Cards`: list, get, create, move, complete, reopen, archive,
  restore, update, assign, attach label
- `client.Labels`: list, create
- `client.Comments`: list, add
- `client.Activities`: list (board / card)

Everything else (custom fields, voting, checklists, recurrence,
automation, integrations, search, calendar, AI) is reachable
through `client.SendAsync(...)` and `SendAsync<T>(...)`. The
`OpenApiSpec` companion doc lists the full surface.

## License

RPL-1.5 — see [`LICENSE`](../../LICENSE) at the repository root.
