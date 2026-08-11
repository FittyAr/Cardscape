# Test implementation plan

1. [x] Add `SourceProjects_HaveOnlyTheApprovedDirectProjectReferences` to parse every `src` project and compare the complete direct-reference graph, including the API-to-Web hosting exception.
2. [x] Add registry tests for resolution, empty registry, invalid empty discriminator, duplicate discriminator, ordinal matching, source snapshotting, and defensive `RegisteredTypes` snapshots.
3. [x] Build and run `Cardscape.ArchitectureTests` and the registry tests narrowly.
4. [x] Re-open tests, review gaps/assertions, and record results in `status.md`.

## Phase 1 follow-up: Seeder authorization and hosted options validation

1. [x] Add `SeederAdminEndpointTests` covering the 401/403 matrix for all four routes.
2. [x] Prove enabled-admin success with 200 for status/options and 202 for run/wipe.
3. [x] Add host-start validation theories for every invalid retention/revocation boundary and a passing-defaults test.
4. [x] Run narrow integration/unit filters, re-open assertions, and record the gap/quality review.
