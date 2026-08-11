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
    private readonly ConcurrentDictionary<string, (string Aggregate, string? Highlight)> _tableMeta = new(StringComparer.OrdinalIgnoreCase);

    private long _startedAtTicks;
    private long _finishedAtTicks;
    private string _status = "Idle";
    private int _currentStep;
    private int _totalSteps;
    private string? _currentStepName;

    public IReadOnlyCollection<SeedLogEntry> Entries => _entries.ToArray();

    public IReadOnlyList<SeedTableStatus> TableSnapshot() =>
        _tableCounts
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv =>
            {
                _tableMeta.TryGetValue(kv.Key, out var meta);
                return new SeedTableStatus(kv.Key, meta.Aggregate ?? kv.Key, kv.Value, meta.Highlight);
            })
            .ToList();

    public string Status => _status;
    public int CurrentStep => _currentStep;
    public int TotalSteps => _totalSteps;
    public string? CurrentStepName => _currentStepName;

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
        _tableMeta.Clear();
        Interlocked.Exchange(ref _startedAtTicks, 0);
        Interlocked.Exchange(ref _finishedAtTicks, 0);
        _status = "Idle";
        _currentStep = 0;
        _totalSteps = 0;
        _currentStepName = null;
    }

    public void MarkStarted(int totalSteps)
    {
        Interlocked.Exchange(ref _startedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        Interlocked.Exchange(ref _finishedAtTicks, 0);
        _status = "Running";
        _totalSteps = totalSteps;
        _currentStep = 0;
        _currentStepName = null;
    }

    public void MarkFinished(string status)
    {
        Interlocked.Exchange(ref _finishedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        _status = status;
    }

    public void SetCurrentStep(int step, string name)
    {
        _currentStep = step;
        _currentStepName = name;
    }

    public void Log(SeedLogEntry entry) => _entries.Enqueue(entry);

    public void RecordTable(string tableKey, long rowCount, string aggregateName, string? highlight = null)
    {
        _tableCounts[tableKey] = rowCount;
        _tableMeta[tableKey] = (aggregateName, highlight);
    }
}
