using System.Collections.Concurrent;

namespace Cardscape.Seeder.Reporting;

/// <summary>
/// In-memory, thread-safe accumulator for everything the seeder
/// emits. The runner and HTTP endpoints receive the same singleton
/// instance through dependency injection. The log stream
/// is exposed to consumers in insertion order via
/// <see cref="Entries"/>.
/// </summary>
public sealed class SeedReport
{
    private readonly ConcurrentQueue<SeedLogEntry> _entries = new();
    private readonly ConcurrentDictionary<string, long> _tableCounts = new(StringComparer.OrdinalIgnoreCase);

    private long _startedAtTicks;
    private long _finishedAtTicks;

    public IReadOnlyCollection<SeedLogEntry> Entries => _entries.ToArray();

    public IReadOnlyList<SeedTableStatus> TableSnapshot() =>
        _tableCounts
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new SeedTableStatus(kv.Key, AggregateName: kv.Key, kv.Value, Highlight: null))
            .ToList();

    public string Status { get; private set; } = "Idle";
    public int CurrentStep { get; private set; }
    public int TotalSteps { get; private set; }
    public string? CurrentStepName { get; private set; }

    public DateTimeOffset? StartedAt =>
        Interlocked.Read(ref _startedAtTicks) == 0
            ? null
            : new DateTimeOffset(Interlocked.Read(ref _startedAtTicks), TimeSpan.Zero);

    public DateTimeOffset? FinishedAt =>
        Interlocked.Read(ref _finishedAtTicks) == 0
            ? null
            : new DateTimeOffset(Interlocked.Read(ref _finishedAtTicks), TimeSpan.Zero);

    public TimeSpan? Elapsed =>
        StartedAt is null
            ? null
            : (FinishedAt ?? DateTimeOffset.UtcNow) - StartedAt.Value;

    public void Reset()
    {
        _entries.Clear();
        _tableCounts.Clear();
        Interlocked.Exchange(ref _startedAtTicks, 0);
        Interlocked.Exchange(ref _finishedAtTicks, 0);
        Status = "Idle";
        CurrentStep = 0;
        TotalSteps = 0;
        CurrentStepName = null;
    }

    public void MarkStarted(int totalSteps)
    {
        Interlocked.Exchange(ref _startedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        Interlocked.Exchange(ref _finishedAtTicks, 0);
        Status = "Running";
        TotalSteps = totalSteps;
        CurrentStep = 0;
        CurrentStepName = null;
    }

    public void MarkFinished(string status)
    {
        Interlocked.Exchange(ref _finishedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        Status = status;
    }

    public void SetCurrentStep(int step, string name)
    {
        CurrentStep = step;
        CurrentStepName = name;
    }

    public void Log(SeedLogEntry entry) => _entries.Enqueue(entry);

    public void RecordTable(string tableKey, long rowCount) => _tableCounts[tableKey] = rowCount;
}
