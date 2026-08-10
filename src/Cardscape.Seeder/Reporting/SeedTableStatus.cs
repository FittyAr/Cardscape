namespace Cardscape.Seeder.Reporting;

/// <summary>Snapshot of how many rows currently sit in a single
/// table. The UI shows one row per tracked table so the operator
/// can see at a glance which tables are empty and which are
/// full after a seed run.</summary>
public sealed record SeedTableStatus(
    string Table,
    string AggregateName,
    long RowCount,
    string? Highlight)
{
    public string DisplayName => string.IsNullOrEmpty(Highlight)
        ? AggregateName
        : $"{AggregateName} — {Highlight}";
}
