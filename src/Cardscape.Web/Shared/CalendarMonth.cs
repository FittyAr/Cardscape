using System.Globalization;

namespace Cardscape.Web.Shared;

/// <summary>
/// A calendar month in the viewer's local time zone. Value object behind
/// every month-paged view (calendar, planner) so the range maths and the
/// label formatting live in one place.
/// </summary>
public readonly record struct CalendarMonth(int Year, int Month)
{
    public static CalendarMonth Current
    {
        get
        {
            DateTime today = DateTime.Now;
            return new(today.Year, today.Month);
        }
    }

    /// <summary>Local midnight of the first day of the month.</summary>
    public DateTimeOffset Start => AtLocalMidnight(new DateTime(Year, Month, 1));

    /// <summary>Exclusive end: local midnight of the first day of the next month.</summary>
    public DateTimeOffset End => Next.Start;

    public CalendarMonth Previous => FromDate(new DateTime(Year, Month, 1).AddMonths(-1));

    public CalendarMonth Next => FromDate(new DateTime(Year, Month, 1).AddMonths(1));

    public bool IsCurrent => this == Current;

    /// <summary>First day as a plain local <see cref="DateTime"/> (what Radzen's scheduler expects).</summary>
    public DateTime FirstDay => new(Year, Month, 1);

    public string Label => FirstDay.ToString("Y", CultureInfo.CurrentCulture);

    private static CalendarMonth FromDate(DateTime date) => new(date.Year, date.Month);

    private static DateTimeOffset AtLocalMidnight(DateTime date) =>
        new(date, TimeZoneInfo.Local.GetUtcOffset(date));
}
