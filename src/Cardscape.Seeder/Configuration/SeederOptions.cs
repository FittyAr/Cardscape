namespace Cardscape.Seeder.Configuration;

/// <summary>
/// Bound from <c>Cardscape:Seeder</c>. The whole seeder pipeline
/// is a no-op when <see cref="Enabled"/> is <c>false</c>; the API
/// returns 404 from the seeder endpoints and the Razor Page
/// refuses to render the "Run" buttons.
/// </summary>
public sealed class SeederOptions
{
    public const string SectionName = "Cardscape:Seeder";

    /// <summary>Master switch. Defaults to <c>true</c> in Development
    /// and <c>false</c> elsewhere; the binding code in
    /// <c>Program.cs</c> is responsible for that default.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>If <c>true</c>, the seeder wipes every row from the
    /// affected tables before inserting the new ones. The wipe
    /// happens in dependency order and skips the migrations table
    /// so the schema is preserved. The flag can be overridden per
    /// call from the UI ("force re-seed" toggle).</summary>
    public bool WipeBeforeSeed { get; set; }

    /// <summary>How many cards to generate per board. Defaults to a
    /// representative number that exercises every UI surface
    /// (cards, comments, votes, checklists) without ballooning the
    /// SQLite file past a few MB.</summary>
    public int CardsPerBoard { get; set; } = 12;

    /// <summary>How many users to create in the demo workspace. The
    /// minimum is 5 (one per persona); the upper bound is what
    /// fits in the demo data set without copy-pasted quirks.</summary>
    public int UserCount { get; set; } = 10;

    /// <summary>Fixed clock for the seed run. Lets us back-date the
    /// demo data to a coherent timeline (activities, due dates,
    /// notifications all line up). Defaults to "now" if null.</summary>
    public DateTimeOffset? FixedNow { get; set; }
}
