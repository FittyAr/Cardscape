namespace Cardscape.Seeder.Simulation;

/// <summary>
/// The simulated calendar the seed plays back. Everything the seeder
/// plants is stamped relative to <see cref="Now"/>, during office hours,
/// so feeds, aging and due-date views look like a team has really been
/// using the product for the last couple of months.
/// </summary>
public sealed class SeedTimeline(DateTimeOffset now, int seed = 20261007)
{
    private const int FirstOfficeHour = 9;
    private const int OfficeHours = 9;

    private readonly Random _random = new(seed);

    public DateTimeOffset Now { get; } = now;

    /// <summary>Simulated history length; the oldest seeded event is roughly this far back.</summary>
    public static int HistoryDays => 75;

    /// <summary>An instant on the given day offset (negative = past) at a random office hour.</summary>
    public DateTimeOffset OnDay(int dayOffset)
    {
        DateTimeOffset at = new DateTimeOffset(Now.UtcDateTime.Date.AddDays(dayOffset), TimeSpan.Zero)
            .AddHours(FirstOfficeHour + _random.Next(OfficeHours))
            .AddMinutes(_random.Next(60));
        return at > Now ? Now.AddMinutes(-_random.Next(5, 90)) : at;
    }

    /// <summary>A due date (end of the working day) on the given day offset; may be in the future.</summary>
    public DateTimeOffset DueOn(int dayOffset) =>
        new DateTimeOffset(Now.UtcDateTime.Date.AddDays(dayOffset), TimeSpan.Zero).AddHours(17);

    /// <summary>A random instant strictly between <paramref name="from"/> and <see cref="Now"/>.</summary>
    public DateTimeOffset Between(DateTimeOffset from, DateTimeOffset? to = null)
    {
        DateTimeOffset end = to ?? Now;
        if (end <= from)
        {
            return from.AddMinutes(1);
        }

        double span = (end - from).TotalMinutes;
        return from.AddMinutes(Math.Max(1, _random.NextDouble() * span));
    }

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public bool Chance(double probability) => _random.NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];
}
