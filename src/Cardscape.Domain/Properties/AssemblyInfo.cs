using System.Runtime.CompilerServices;

// Seeder project — needs the internal factory methods
// (BoardMember.Create, WorkspaceMember.Create, CardMember.Create,
// ChecklistItem.Create) to construct join rows without going through
// the public aggregate APIs. The factories are deliberately internal
// so the application layer is the only normal writer; the Seeder is
// a tool, not a production code path, so widening access here is
// the cheaper alternative to duplicating the join-row constructors
// in a public surface.
[assembly: InternalsVisibleTo("Cardscape.Seeder")]
