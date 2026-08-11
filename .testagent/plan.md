# Test implementation plan

1. [x] Add `SourceProjects_HaveOnlyTheApprovedDirectProjectReferences` to parse every `src` project and compare the complete direct-reference graph, including the API-to-Web hosting exception.
2. [x] Add registry tests for resolution, empty registry, invalid empty discriminator, duplicate discriminator, ordinal matching, source snapshotting, and defensive `RegisteredTypes` snapshots.
3. [x] Build and run `Cardscape.ArchitectureTests` and the registry tests narrowly.
4. [x] Re-open tests, review gaps/assertions, and record results in `status.md`.
