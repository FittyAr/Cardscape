# Test research

## Bounded target inventory

- `tests/Cardscape.ArchitectureTests/ArchitectureTests.cs`: existing NetArchTest rules inspect compiled type dependencies, not the effective `ProjectReference` graph.
- `src/*/*.csproj`: seven production projects whose direct references form the architecture graph.
- `src/Cardscape.Infrastructure/BackgroundJobs/BackgroundJobHandlerRegistry.cs`: immutable registry built once from DI handlers.
- `tests/Cardscape.UnitTests`: xUnit + FluentAssertions conventions, with global imports.

## Existing conventions

- Test framework: xUnit (`[Fact]`).
- Assertions: FluentAssertions.
- Test names: `Member_Condition_ExpectedResult`.
- Architecture tests live in `Cardscape.ArchitectureTests`; infrastructure unit tests live under `Cardscape.UnitTests/Infrastructure`.

## Acceptance checklist

- [ ] Assert the complete effective direct `ProjectReference` graph for every project under `src`.
- [ ] Document and preserve the deliberate `Cardscape.Api -> Cardscape.Web` reference used to host Blazor WASM.
- [ ] Detect an invalid future project reference even when no type from it is used.
- [ ] Registry resolves a registered handler and exposes its discriminator.
- [ ] Registry rejects null/empty/whitespace handler types.
- [ ] Registry rejects duplicate types and uses ordinal discriminator identity.
- [ ] Build and run the two narrow test projects.
- [ ] Review assertion strength and behavior gaps.
