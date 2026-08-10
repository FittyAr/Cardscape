using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>One step of the seeding pipeline. Each step is
/// responsible for inserting the rows of one logical area
/// (users, boards, cards, etc.) using the domain's public
/// factory methods so the persisted state respects every
/// invariant the aggregate enforces.</summary>
public interface ISeedStep
{
    /// <summary>Human-friendly name. Surfaced in the UI's
    /// step-progress column and used as the
    /// <see cref="SeedLogEntry.Step"/> for every log line
    /// emitted by this step.</summary>
    string Name { get; }

    /// <summary>Display order. The runner sorts by this value
    /// so the dependency order (Users → Workspaces → Boards →
    /// Cards → …) is stable and obvious from the code.</summary>
    int Order { get; }

    /// <summary>Executes the step. Implementations append to
    /// the appropriate <see cref="SeedContext"/> collections
    /// and use <paramref name="log"/> to surface progress to
    /// the operator. They do not call
    /// <c>SaveChanges</c>; the runner owns the unit of
    /// work so a single failure rolls back every step.</summary>
    Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken);
}
