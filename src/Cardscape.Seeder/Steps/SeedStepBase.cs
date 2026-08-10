using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Common helpers every <see cref="ISeedStep"/>
/// implementation can lean on. Keeps the per-step classes
/// short.</summary>
public abstract class SeedStepBase : ISeedStep
{
    public abstract string Name { get; }
    public abstract int Order { get; }
    public abstract Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken);

    protected void Log(SeedReport log, SeedLogLevel level, string message) =>
        log.Log(new SeedLogEntry(DateTimeOffset.UtcNow, level, Name, message));
}
